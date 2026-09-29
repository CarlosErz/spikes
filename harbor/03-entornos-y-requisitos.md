# 3. Entornos donde funciona y requisitos

## Dónde SÍ funciona

| Entorno | Soporte | Cómo se instala | Notas |
|---|---|---|---|
| **Servidor Linux físico o VM** (Ubuntu, Debian, RHEL, Rocky, AlmaLinux, SUSE…) | ✅ Oficial | Instalador *online* u *offline* + Docker Compose | Lo más común para empezar. Cualquier distro con Docker Engine y Compose v2. |
| **VM Linux en nube** (Azure VM, AWS EC2, GCP GCE, DigitalOcean…) | ✅ Oficial | Igual que arriba | Puedes usar el object storage de esa nube (Blob/S3/GCS) para los blobs. |
| **Kubernetes** (vanilla, k3s, RKE2, OpenShift, AKS, EKS, GKE, Tanzu) | ✅ Oficial | **Helm chart** `goharbor/harbor` | La opción para **HA** y escala. Requiere Ingress, StorageClass y cert. |
| **Entornos air‑gapped** (sin internet) | ✅ Oficial | Instalador **offline** (`.tgz` con todas las imágenes) | Trivy necesita su base de CVEs cargada manualmente o desde un mirror. |
| **Edge / sucursales** | ✅ | Harbor pequeño por sitio + replicación desde el central | Patrón hub‑and‑spoke. |

**Arquitectura de CPU:** los binarios e imágenes oficiales del instalador están pensados para
**x86_64 (amd64)**. El soporte ARM64 ha sido históricamente limitado/comunitario; si el servidor
es ARM (p. ej. Graviton, Ampere, Raspberry Pi), **verifícalo contra la versión que vayas a usar**
antes de comprometerte.

## Dónde NO (o no para producción)

| Entorno | Estado |
|---|---|
| **Windows Server / Windows 11 como host** | ❌ No soportado. Los contenedores de Harbor son Linux. |
| **Docker Desktop (Windows/macOS) o WSL2** | ⚠️ Puede arrancar para *jugar*, pero no es soportado ni apto para producción. Para el laboratorio usa una VM Linux (Hyper‑V, VirtualBox, VM en nube). |
| **Podman en lugar de Docker** | ⚠️ El instalador asume Docker Engine + Docker Compose. No es la ruta soportada. |
| **Cliente** | ✅ Cualquier SO: Windows, macOS o Linux pueden hacer `push/pull` contra Harbor. La restricción es solo para el **servidor**. |

## Requisitos de hardware (instalación Compose)

| Recurso | Mínimo | Recomendado |
|---|---|---|
| CPU | 2 núcleos | 4 núcleos |
| RAM | 4 GB | 8 GB |
| Disco | 40 GB | 160 GB+ |

El disco crece con tus imágenes: calcula **(tamaño promedio de imagen) × (tags que conservas) ×
(repositorios)**, menos lo que se deduplica entre capas. Con políticas de retención + GC se
mantiene bajo control. Si usas S3/Blob, el disco local solo necesita logs, BD y SO.

## Requisitos de software

| Componente | Versión |
|---|---|
| Docker Engine | > 20.10 |
| Docker Compose | > 2.3 (plugin `docker compose`) |
| OpenSSL | Reciente (para generar certificados) |

## Puertos

| Puerto | Uso |
|---|---|
| **443** (HTTPS) | UI, API y registro. **Úsalo siempre en producción.** |
| **80** (HTTP) | UI/API; normalmente solo redirige a 443. |
| 9090 (opcional) | Métricas Prometheus si habilitas `metric`. |

Salidas a internet (si no es air‑gapped): Docker Hub u otros registros para proxy caché y
replicación, y `ghcr.io`/mirror para la base de datos de Trivy.

## Otros requisitos

- **Nombre DNS** (ej. `harbor.miempresa.com`) y **certificado TLS** válido (de una CA pública,
  corporativa o autofirmado distribuido a los clientes).
- **Backends de almacenamiento soportados** para blobs: sistema de archivos local/NFS, Amazon S3
  (y compatibles como MinIO), Azure Blob, Google Cloud Storage, OpenStack Swift, Alibaba OSS.
- **Base de datos**: la interna (contenedor PostgreSQL) sirve para un nodo; en HA se usa una
  PostgreSQL externa administrada.

Sigue en [04-instalacion-paso-a-paso.md](04-instalacion-paso-a-paso.md).
