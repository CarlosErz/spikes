# 1. ¿Qué es Harbor?

**Harbor** es un **registro de artefactos cloud‑native, open source**, que almacena, firma y
escanea imágenes de contenedores y otros artefactos OCI. Se instala en infraestructura propia
(on‑premise o en tus VMs de nube) y agrega capas de seguridad, identidad y gestión sobre el
registro base de Docker.

| Dato | Valor |
|---|---|
| Sitio | <https://goharbor.io> |
| Código | <https://github.com/goharbor/harbor> |
| Licencia | Apache 2.0 (gratis, uso comercial permitido) |
| Origen | Creado por VMware (China R&D) en 2014, liberado como open source en 2016 |
| Gobernanza | Donado a la **CNCF** en 2018; **graduado** en junio de 2020 |
| Lenguaje | Go (backend) + Angular (UI) |
| Versión vigente | Línea **v2.15.x** (2026) |

## Qué problema resuelve

El registro oficial de Docker (`distribution/registry`, hoy proyecto CNCF "Distribution") solo
sabe guardar y servir capas. No tiene UI, usuarios, permisos finos, escaneo ni políticas. Harbor
**envuelve** ese registro y agrega todo lo que una organización necesita para usarlo en serio.

## Funcionalidades principales

### Organización y acceso
- **Proyectos**: agrupan repositorios. Pueden ser **públicos** (pull anónimo) o **privados**.
- **RBAC por proyecto** con roles: *Project Admin, Maintainer, Developer, Guest, Limited Guest*.
- **Autenticación**: base de datos local, **LDAP/Active Directory** u **OIDC** (Keycloak, Entra ID,
  Google, Okta, Dex…). Grupos de LDAP/OIDC se pueden mapear a roles.
- **Robot accounts**: cuentas técnicas con permisos acotados y expiración, para CI/CD.
- **Cuotas** de almacenamiento por proyecto.

### Seguridad de la cadena de suministro
- **Escaneo de vulnerabilidades** con **Trivy** incluido (scanner "pluggable": se pueden conectar
  otros vía la Scanner API). Escaneo al subir, programado o manual.
- **Generación de SBOM** (lista de componentes de la imagen).
- **Políticas de despliegue**: impedir el pull de imágenes con vulnerabilidades por encima de una
  severidad, o **sin firma**.
- **Firmas**: soporta artefactos firmados con **Cosign** (Sigstore) y **Notation** (Notary v2).
  *Notary v1 / Docker Content Trust se eliminó en v2.9.*
- **Tags inmutables**: una regla impide sobrescribir `1.4.0` con otro contenido.
- **Auditoría**: log de quién hizo push/pull/borrado y cuándo.

### Distribución
- **Replicación** push/pull, manual, programada o por evento, entre Harbor y: otro Harbor,
  Docker Hub, AWS ECR, Azure ACR, Google GCR/Artifact Registry, Quay, GitLab, JFrog Artifactory,
  Alibaba ACR, Huawei SWR, entre otros. Filtros por nombre, tag o etiqueta.
- **Proxy caché**: un proyecto que actúa como espejo de Docker Hub (u otro registro). El primer
  pull baja de internet; los siguientes salen de Harbor. Evita el rate limit de Docker Hub.
- **Precalentamiento P2P** con Dragonfly o Kraken para clusters grandes.

### Ciclo de vida y almacenamiento
- **Políticas de retención**: "conserva los últimos 10 tags de cada repo", "borra lo no usado en
  90 días", etc.
- **Garbage collection (GC)**: libera el espacio de capas que ya nadie referencia.
- **Artefactos OCI**: imágenes multi‑arquitectura, **charts de Helm como OCI**, CNAB, WASM, etc.
  *ChartMuseum (el repositorio Helm clásico) se eliminó en v2.8; Helm ahora se usa vía OCI.*

### Operación
- **UI web**, **API REST v2** documentada con Swagger, webhooks (Slack, HTTP, CloudEvents).
- **Métricas Prometheus** y **tracing OpenTelemetry**.
- Proveedor de Terraform comunitario y operadores/Helm chart oficiales para Kubernetes.

## Novedades recientes (resumen)

- **v2.15.0**: opción de borrar tags en GC, límite de conexiones hacia registros upstream, CA por
  endpoint de registro, soporte formato de bundle Cosign v3, Trivy actualizado.
- **v2.15.2**: actualiza la BD interna de **PostgreSQL 15 → 18** (con `pg_upgrade` automático al
  arrancar) y reemplaza **Redis por Valkey** como caché. *Haz backup antes de actualizar.*

Consulta siempre las notas de versión: <https://github.com/goharbor/harbor/releases>.

Sigue en [02-arquitectura-y-funcionamiento.md](02-arquitectura-y-funcionamiento.md).
