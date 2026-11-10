# Spike: en qué formato le llega al API una importación de catálogo

**Pregunta:** para importar hasta 5000 filas del catálogo de servicios (Tecno Services, HU-04-010),
¿conviene subir el CSV crudo o que el navegador lo lea y mande JSON? ¿Cuánto cuesta cada opción y
hay algo mejor?

**Respuesta corta:** el formato casi no cambia el costo. Lo que sí lo cambia es **comprimir el
body**: con gzip, el JSON de 1.4 MB viaja en 80 KB. ASP.NET Core trae lo necesario para recibirlo y
corta una bomba de descompresión por sí solo. La decisión quedó en el ADR 0022 de
`tecno-services-documents`.

Medido el 2026-11-10 en Windows 11, 20 núcleos, .NET 10.0.9 y Node 24.16.0. Los tiempos son de esta
máquina. En un servidor chico multiplica por 2 o 3, y en un celular barato por 4 a 6.

## 1. Servidor: tamaño y costo de leer cada formato

`bench/` (`dotnet run -c Release -- 5000`). Lee 5000 filas de servicio de cada formato hasta tener
los objetos tipados. Se toma la mediana de 200 lecturas después de calentar.

| Formato | Crudo | gzip | brotli | Lectura p50 | p95 | Memoria asignada |
|---|---|---|---|---|---|---|
| CSV multipart (CsvHelper 33) | 554 KB | 82 KB | 89 KB | 3.4 ms | 4.4 ms | 3.2 MB |
| **JSON, objeto por fila** | 1387 KB | 96 KB | 106 KB | 3.8 ms | 12.1 ms | 2.7 MB |
| JSON compacto (columnas + arreglos) | 654 KB | 85 KB | 95 KB | 16.0 ms | 28.9 ms | 13.8 MB |
| NDJSON (una línea por fila) | 1387 KB | 96 KB | 106 KB | 3.2 ms | 3.4 ms | 4.8 MB |
| MessagePack (arreglos) | 524 KB | 83 KB | 83 KB | 1.8 ms | 3.1 ms | 2.6 MB |

Con 500 filas todos tardan menos de 1 ms, salvo el JSON compacto, que tarda 2 ms.

**Lectura:**
- **Leer cualquier formato cuesta milisegundos.** Con 1000 importaciones al mes, la diferencia entre
  el más rápido y el más lento son segundos de CPU **al mes**.
- **Comprimido, el tamaño se empareja.** Todos quedan entre 82 y 106 KB. Las claves que el JSON
  repite en cada fila son justo lo que gzip comprime mejor.
- **El JSON compacto es el peor.** Ahorra bytes sin comprimir, pero lee 4 veces más lento, usa 5
  veces más memoria y obliga a validar a mano cada posición. El costo está en el código de lectura,
  no en el formato.
- **MessagePack es el más rápido**, pero gana 2 ms y pide una librería en el servidor y otra en el
  navegador. Además, en las herramientas de desarrollo del navegador el body se ve como binario.

## 2. Navegador: leer el CSV, validar y armar el JSON

`bench/cliente.mjs` (`node cliente.mjs 5000`). Es un lector RFC 4180 propio de ~25 líneas, sin
dependencias, más la validación de enums, decimales, caracteres de control y bidireccionales, y
`JSON.stringify`.

| Filas | CSV | JSON | JSON en gzip (`CompressionStream`) | Leer CSV | Leer + validar + JSON |
|---|---|---|---|---|---|
| 5000 | 552 KB | 1371 KB | 80 KB | 2.6 ms | 13.4 ms |
| 500 | 55 KB | 137 KB | 9 KB | 0.3 ms | 1.0 ms |

En un celular barato serían de 50 a 80 ms para 5000 filas: el usuario no lo nota y no hace falta un
Web Worker.

## 3. Lo que sí cuesta: el tiempo de subida del usuario

Se calcula con 4G lenta (~5 Mbps de subida) y dos envíos por importación (vista previa y aplicar):

| Body | Por envío | Por importación |
|---|---|---|
| CSV crudo | ~0.9 s | ~1.8 s |
| JSON por objetos | ~2.2 s | ~4.4 s |
| **JSON por objetos + gzip** | **~0.13 s** | **~0.3 s** |

La subida no genera costo de infraestructura. La entrada de datos no se cobra, y el despliegue
previsto (Cloudflare Tunnel) no cobra ancho de banda. El costo es el tiempo del usuario en campo.
Lo que **no** se midió es el trabajo en la base de datos (buscar claves, insertar y actualizar), que
sigue siendo lo más caro de la importación. Se mide cuando exista el endpoint.

## 4. Lo nuevo: gzip en la petición, y la bomba de descompresión

`descompresion/` (`dotnet run -c Release`) levanta Kestrel con `AddRequestDecompression()` y un
endpoint con `[RequestSizeLimit(4 MB)]`, y le manda peticiones:

| Caso | Enviado | Descomprimido | Resultado |
|---|---|---|---|
| JSON sin comprimir | 566 KB | 566 KB | 200 |
| El mismo JSON en gzip | **32 KB** | 566 KB | 200 |
| Bomba: 200 MB de ceros en gzip | 199 KB | 200 MB | **413**: *"The decompressed request body is larger than the request body size limit"* |
| Bomba de 4 MB + 1 byte | 4 KB | 4 MB | **413** |
| Sin comprimir, arriba del límite | 4 MB | 4 MB | 413 (Kestrel) |

**El límite se cuenta sobre lo descomprimido**, y el middleware lee como flujo, sin cargar todo en
memoria. La defensa contra la bomba viene de fábrica, siempre que el endpoint tenga su propio límite.

**Trampa encontrada en `Tecno-Services-Api` (`develop`):** el `ExceptionMiddleware` no reconoce
`BadHttpRequestException`. Un body demasiado grande, comprimido o no, hoy sale como **500
`UNEXPECTED_ERROR`** y queda en el log como error inesperado. Hay que traducirlo a su propio código
(413) antes de abrir cualquier endpoint de importación.

## 5. Estándares y trabajos que respaldan la decisión

| Fuente | Qué aporta aquí |
|---|---|
| RFC 4180 (CSV) | El dialecto que acepta el lector: coma, comillas dobles y `""` escapado |
| RFC 8259 (JSON) | El body. Objetos con nombre: el validador de siempre, sin validar posiciones a mano |
| RFC 9110, sección 8.4 (`Content-Encoding`) | Comprimir el cuerpo de una **petición** es HTTP estándar, no un truco |
| WHATWG *Compression Streams* | `CompressionStream('gzip')` nativo en Chrome, Edge, Firefox y Safari 16.4+, sin librería |
| W3C *CSV on the Web* (2015) y Frictionless *Table Schema* | Formato estándar para describir columnas, tipos y restricciones. Sirve para el endpoint de esquema en lugar de inventar uno |
| OWASP *CSV Injection* | El saneador contra fórmulas del reporte de rechazos |
| OWASP *Mass Assignment* | Rechazar campos que no existen (`UnmappedMemberHandling.Disallow`) |
| van den Burg et al., *Wrangling Messy CSV Files…* (2019), y Vitagliano et al., *Pollock* (PVLDB 2023) | Muestran que los cargadores de CSV fallan sobre todo por el dialecto. Por eso se fija uno y se rechaza el resto en vez de adivinarlo |
| Langdale y Lemire, *Parsing Gigabytes of JSON per Second* (VLDB Journal 2019) | Leer JSON va a velocidades de GB/s. Con 1.4 MB el formato no es el cuello de botella, y las mediciones lo confirman |

Las referencias se citan de memoria: hay que verificar los enlaces antes de aceptar el ADR.

## Cómo reproducir

```bash
cd bench && dotnet run -c Release -- 5000 && node cliente.mjs 5000
cd ../descompresion && dotnet run -c Release
```
