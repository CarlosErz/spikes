# Spike: Harbor — registro de contenedores para servidores Linux

> **Tipo:** spike de investigación · **Fecha:** 2026-09-29 · **Estado:** terminado
> **Versión de referencia:** Harbor v2.15.x (la línea estable vigente al escribir esto).
> Verifica la última en <https://github.com/goharbor/harbor/releases> antes de instalar.

## Pregunta que responde este spike

¿Qué es Harbor, cómo funciona, en qué entornos corre, qué gana y qué cuesta adoptarlo como
registro de imágenes propio en servidores Linux?

## Respuesta corta (TL;DR)

- **Harbor es un registro de contenedores (como Docker Hub, pero tuyo)** que instalas en tus
  propios servidores. Guarda imágenes Docker/OCI, charts de Helm y otros artefactos OCI.
- Encima del registro "pelón" (`distribution/registry`) agrega lo que una empresa necesita:
  **usuarios y permisos (RBAC), login con LDAP/AD/OIDC, escaneo de vulnerabilidades (Trivy),
  firma de imágenes (Cosign/Notation), replicación entre registros, proxy caché de Docker Hub,
  cuotas, retención, inmutabilidad de tags, auditoría y una UI web.**
- Es **open source (Apache 2.0)** y **proyecto graduado de la CNCF** (el mismo nivel que
  Kubernetes o Prometheus). Sin costo de licencia.
- **Corre solo en Linux**, de dos formas: con **Docker Compose** en un servidor (instalador
  oficial) o con **Helm en Kubernetes** (para alta disponibilidad).
- Mínimo: **2 CPU, 4 GB RAM, 40 GB disco**. Recomendado: **4 CPU, 8 GB RAM, 160 GB+**.
- **Pro principal:** control total, seguridad de la cadena de suministro y ahorro en
  descargas/egreso. **Contra principal:** lo operas tú (actualizaciones, backups, certificados,
  disco, HA).

**Recomendación:** adoptarlo si necesitamos imágenes privadas on‑premise, air‑gapped, sin límites
de Docker Hub o con políticas de seguridad (bloquear imágenes vulnerables o sin firmar). Si ya
vivimos en una nube y solo necesitamos guardar imágenes, el registro administrado de esa nube
(ACR/ECR/GAR) o GHCR es menos trabajo. Detalle en [06-beneficios-pros-contras.md](06-beneficios-pros-contras.md).

## Cómo leer esta carpeta (de 0 a operar)

| # | Archivo | Qué aprendes |
|---|---|---|
| 0 | [00-conceptos-previos.md](00-conceptos-previos.md) | Qué es una imagen, un registro, un tag, OCI. **Empieza aquí si nunca has usado un registro.** |
| 1 | [01-que-es-harbor.md](01-que-es-harbor.md) | Qué es Harbor, historia, qué problema resuelve, funcionalidades |
| 2 | [02-arquitectura-y-funcionamiento.md](02-arquitectura-y-funcionamiento.md) | Componentes internos y qué pasa en un `docker push` / `pull` |
| 3 | [03-entornos-y-requisitos.md](03-entornos-y-requisitos.md) | Dónde funciona y dónde no, hardware, puertos, almacenamiento |
| 4 | [04-instalacion-paso-a-paso.md](04-instalacion-paso-a-paso.md) | Laboratorio: instalar Harbor en un Ubuntu/RHEL con Docker Compose |
| 5 | [05-operacion-diaria.md](05-operacion-diaria.md) | Push/pull, robots, proxy caché, replicación, GC, backups, upgrades, troubleshooting |
| 6 | [06-beneficios-pros-contras.md](06-beneficios-pros-contras.md) | Beneficios, pros, contras, alternativas y cuándo sí / cuándo no |
| 7 | [07-glosario-y-fuentes.md](07-glosario-y-fuentes.md) | Glosario y enlaces a la documentación oficial |

## Siguientes pasos sugeridos

1. Levantar el laboratorio del paso 4 en una VM Linux de prueba (no en Windows: ver paso 3).
2. Subir la imagen de `monolito-modular-template` a un proyecto privado y escanearla.
3. Decidir: Compose en una VM (simple) vs. Helm en Kubernetes (HA), y dónde viven los blobs
   (disco local vs. S3/MinIO/Azure Blob).
