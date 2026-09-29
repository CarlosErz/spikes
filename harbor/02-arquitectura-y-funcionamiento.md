# 2. Arquitectura y funcionamiento

Harbor no es un solo binario: es un conjunto de **contenedores** que colaboran. Con el
instalador de Docker Compose verás algo así en `docker ps`:

```
                         ┌─────────────────────────────┐
  docker / podman /      │         harbor (nginx)       │  :80 / :443
  helm / navegador ─────▶│   proxy de entrada + TLS     │
                         └──────┬─────────────┬────────┘
                                │             │
                   /api, /c, UI │             │ /v2/ (protocolo OCI)
                                ▼             ▼
                   ┌──────────────┐    ┌──────────────┐     ┌─────────────────┐
                   │ harbor-portal│    │ harbor-core  │────▶│    registry     │
                   │   (UI web)   │    │ auth, RBAC,  │     │ (distribution)  │
                   └──────────────┘    │ API, políticas│     │ guarda blobs    │
                                       └──┬───┬───┬───┘     └────────┬────────┘
                                          │   │   │                  │
                     ┌────────────────────┘   │   └────────┐         ▼
                     ▼                        ▼            ▼   ┌───────────────────┐
             ┌──────────────┐      ┌──────────────┐  ┌──────────┐│ Almacenamiento:  │
             │ harbor-db    │      │ redis/valkey │  │jobservice││ disco local, S3, │
             │ (PostgreSQL) │      │ caché + colas│  │ trabajos ││ Azure Blob, GCS, │
             │ metadatos    │      └──────────────┘  │ async    ││ Swift, OSS...    │
             └──────────────┘                        └────┬─────┘└───────────────────┘
                                                          │
                                                          ▼
                                                  ┌──────────────┐
                                                  │ trivy-adapter│  escaneo de CVEs
                                                  └──────────────┘
```

## Componentes

| Contenedor | Rol |
|---|---|
| **nginx** (`nginx`/`harbor-nginx`) | Punto de entrada único. Termina TLS y enruta: `/v2/` al registro (vía core), el resto a portal/core. |
| **harbor-portal** | La UI web (Angular). |
| **harbor-core** | El cerebro: API REST, autenticación (local/LDAP/OIDC), emisión de tokens para el registro, RBAC, cuotas, políticas, webhooks, proxy caché. |
| **registry** | El registro Distribution original. Guarda y sirve manifests y blobs. |
| **registryctl** | Controlador auxiliar del registro (lo usa el GC). |
| **harbor-db** | PostgreSQL con **metadatos**: usuarios, proyectos, artefactos, tags, resultados de escaneo, auditoría. *No* guarda las capas. |
| **redis** / **valkey** | Caché y cola de trabajos. (Valkey reemplaza a Redis desde v2.15.2.) |
| **harbor-jobservice** | Ejecuta trabajos asíncronos: replicación, escaneo, GC, retención, webhooks. |
| **trivy-adapter** | Escáner de vulnerabilidades (si instalas con `--with-trivy`). Descarga su base de CVEs de internet (o se alimenta offline). |
| **harbor-log** | Recolecta logs (rsyslog) en `/var/log/harbor/` del host. |
| **harbor-exporter** | Métricas Prometheus (si se habilita `metric`). |

**Clave para entender la operación:** los **metadatos** viven en PostgreSQL y los **blobs**
(capas, que es lo pesado) viven en el *storage backend*. Un backup necesita **ambos**.

## Qué pasa en un `docker push`

1. `docker login harbor.miempresa.com` → el cliente manda usuario/contraseña (o token de robot).
2. `docker push harbor.miempresa.com/backend/api:1.0`:
   1. El cliente llama `/v2/` y recibe `401` con la dirección del servicio de tokens de **core**.
   2. Pide un **token JWT** a core; core valida identidad y que tenga rol ≥ *Developer* en el
      proyecto `backend`, y revisa la **cuota**.
   3. Con el token, sube cada **capa** que el registro no tenga (las compartidas se saltan).
   4. Sube el **manifest**. Core registra el artefacto y el tag en PostgreSQL.
3. Eventos posteriores (en jobservice): **escaneo automático** si el proyecto lo tiene activado,
   **replicación por evento**, **webhooks**, verificación de **tag inmutable**.

## Qué pasa en un `docker pull`

1. Token igual que arriba (o anónimo si el proyecto es público).
2. Core aplica **políticas** antes de servir el manifest:
   - ¿El proyecto exige firma y la imagen no está firmada? → rechaza.
   - ¿Tiene CVEs de severidad ≥ umbral configurado? → rechaza.
3. El registro sirve manifest y capas (o redirige al storage si es S3 con redirect habilitado).
4. Se actualiza el "último pull" (útil para retención).

## Proxy caché

Si el proyecto `dockerhub-cache` es un proxy de Docker Hub:

```
docker pull harbor.miempresa.com/dockerhub-cache/library/nginx:1.27
```

Harbor revisa si la tiene; si no, la baja de Docker Hub, la guarda y la sirve. Las siguientes
máquinas la obtienen localmente.

## Alta disponibilidad

- **Docker Compose**: una sola máquina, **sin HA**. Si cae el servidor, cae el registro.
- **Kubernetes (Helm)**: varias réplicas de core/portal/registry/jobservice, con **PostgreSQL y
  Redis/Valkey externos en HA** y **almacenamiento compartido** (S3/objeto o un PVC `ReadWriteMany`).

Sigue en [03-entornos-y-requisitos.md](03-entornos-y-requisitos.md).
