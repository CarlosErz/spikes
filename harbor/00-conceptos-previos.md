# 0. Conceptos previos (desde cero)

Si ya sabes qué es una imagen y un registro, salta a [01-que-es-harbor.md](01-que-es-harbor.md).

## Imagen de contenedor

Un paquete inmutable con tu aplicación y todo lo que necesita para correr (runtime, librerías,
archivos). Se construye con `docker build` a partir de un `Dockerfile`. Por dentro es:

- **Capas (layers):** archivos comprimidos (`.tar.gz`), cada una es un cambio sobre la anterior.
  Se identifican por su hash (`sha256:...`). Si dos imágenes comparten capa, se guarda una vez.
- **Config:** metadatos (variables de entorno, comando de arranque, arquitectura).
- **Manifest:** un JSON que lista la config y las capas. Su hash es el **digest** de la imagen.

## Registro (registry)

Un servidor HTTP que **guarda y sirve imágenes**. Tú haces `docker push` para subir y
`docker pull` para bajar. Ejemplos: Docker Hub, GitHub Container Registry (GHCR), Azure ACR,
AWS ECR… y **Harbor**, que es uno que instalas tú.

## Nombre de una imagen

```
harbor.miempresa.com / backend / api-identity : 1.4.0
└──── registro ────┘   └proyecto┘ └repositorio┘ └─tag─┘
```

- **Repositorio:** la "carpeta" de una imagen con todas sus versiones.
- **Tag:** etiqueta legible y **movible** (`latest`, `1.4.0`). Puede apuntar a otro contenido mañana.
- **Digest:** `@sha256:ab12…`, identifica el contenido exacto y **nunca cambia**. En producción
  conviene desplegar por digest o usar tags inmutables.

## OCI

**Open Container Initiative**: el estándar abierto del formato de imagen y del protocolo de
distribución (la API HTTP `/v2/...`). Gracias a él, cualquier cliente (`docker`, `podman`,
`containerd`, `helm`, `oras`) habla con cualquier registro compatible. Un registro OCI no solo
guarda imágenes: también **artefactos** (charts de Helm, firmas, SBOMs, módulos WASM…).

## ¿Por qué un registro propio?

- Imágenes **privadas** que no pueden salir a internet.
- **Límites de descarga** de Docker Hub (rate limits) que rompen pipelines.
- **Velocidad**: el registro vive en la misma red que tus servidores.
- **Seguridad**: saber qué vulnerabilidades tiene lo que despliegas y quién lo subió.
- Entornos **sin internet** (air‑gapped).

Harbor resuelve todo eso. Sigue en [01-que-es-harbor.md](01-que-es-harbor.md).
