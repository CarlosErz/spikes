# 6. Beneficios, pros, contras y alternativas

## Beneficios

- **Soberanía de datos**: las imágenes (y el código que contienen) no salen de tu infraestructura.
- **Seguridad de la cadena de suministro** integrada: escaneo, SBOM, firmas y políticas que
  bloquean lo inseguro *antes* de que llegue a producción.
- **Independencia de Docker Hub**: el proxy caché elimina los problemas de rate limit y acelera
  builds.
- **Costos predecibles**: sin licencias ni cobro por GB descargado; pagas servidor y disco.
- **Estándar abierto**: habla OCI, así que funciona con Docker, Podman, containerd, Kubernetes,
  Helm, ORAS, Cosign. Sin lock‑in.
- **Gobierno**: RBAC, SSO corporativo, cuotas y auditoría para cumplimiento (ISO 27001, SOC 2…).

## Pros

| Pro | Detalle |
|---|---|
| Gratis y open source | Apache 2.0; proyecto **graduado CNCF**, comunidad grande y activa |
| Todo en uno | Registro + UI + seguridad + replicación en una sola instalación |
| Instalación simple | Un `harbor.yml` y `install.sh` en una VM; operativo en ~30 min |
| Funciona sin internet | Instalador offline, ideal para air‑gapped |
| Multi‑registro | Replicación con Docker Hub, ECR, ACR, GAR, Quay, GitLab, Artifactory… |
| Escala | De una VM pequeña a Kubernetes HA con storage de objetos |
| SSO | LDAP/AD y OIDC (Entra ID, Keycloak, Okta…) |
| API completa | Automatizable (REST, webhooks, Terraform) |

## Contras

| Contra | Detalle / mitigación |
|---|---|
| **Lo operas tú** | Actualizaciones, parches de seguridad, certificados, monitoreo y backups son responsabilidad del equipo. |
| **Sin HA en Compose** | Una VM = punto único de falla. HA real exige Kubernetes + PostgreSQL/Redis externos + storage de objetos → mucha más complejidad. |
| **Consumo de recursos** | ~10 contenedores; 4–8 GB de RAM. Pesado comparado con un `registry:2` pelón. |
| **Solo Linux** como servidor | No corre en Windows Server; en Docker Desktop solo para pruebas. |
| **Gestión de TLS** | Docker exige HTTPS; distribuir CAs autofirmadas a todos los clientes es tedioso. Mejor un certificado de CA corporativa o pública. |
| **Disco** | Crece rápido sin retención + GC. El GC es otra tarea a programar y vigilar. |
| **Upgrades con cuidado** | Migraciones de esquema y cambios de componentes (ChartMuseum y Notary v1 eliminados; PostgreSQL 15→18 y Redis→Valkey en 2.15.2). Siempre backup y leer notas. |
| **Solo artefactos OCI** | No es un gestor universal de paquetes: **no** guarda NuGet, npm, Maven ni PyPI (para eso: Nexus, Artifactory). |
| **Soporte comercial** | No hay soporte oficial del proyecto; depende de la comunidad o de terceros/distribuciones que lo incluyan. |
| **Trivy necesita internet** para actualizar la BD de CVEs, o un proceso manual en air‑gapped. |

## Alternativas

| Opción | Tipo | Cuándo preferirla sobre Harbor |
|---|---|---|
| `registry:2` / CNCF Distribution | Self‑hosted, mínimo | Solo necesitas guardar imágenes en un lab, sin UI ni usuarios |
| **Azure ACR / AWS ECR / Google Artifact Registry** | SaaS de la nube | Ya todo corre en esa nube; quieres cero operación y HA incluida (pagas por GB/transferencia) |
| **GitHub Container Registry (GHCR)** / **GitLab Registry** | SaaS integrado a Git | El código ya está ahí y el CI también; volumen moderado |
| **Docker Hub** (planes pagados) | SaaS | Imágenes públicas o equipos pequeños |
| **Sonatype Nexus** / **JFrog Artifactory** | Self‑hosted/SaaS, universal | Necesitas **además** NuGet/npm/Maven/PyPI en el mismo producto (Artifactory completo es de pago) |
| **Red Hat Quay** / Project Quay | Self‑hosted | Ecosistema OpenShift/Red Hat |
| **Zot** | Self‑hosted, ligero, OCI puro | Edge o recursos muy limitados, sin tantas funciones empresariales |

## ¿Cuándo sí y cuándo no?

**Adoptar Harbor si:**
- Hay servidores **on‑premise** o requerimiento de que las imágenes no salgan de la red.
- Hay entornos **air‑gapped** o con internet limitado.
- Nos pega el **rate limit** de Docker Hub o queremos builds más rápidos.
- Queremos **políticas de seguridad** (no desplegar imágenes con CVEs críticas o sin firma).
- Operamos **multi‑nube** o híbrido y queremos un registro neutral con replicación.

**No adoptarlo (o todavía no) si:**
- Todo corre en una sola nube y el registro administrado cubre la necesidad → menos operación.
- El equipo no tiene capacidad de operar un servicio más (parches, backups, guardia).
- Lo que se necesita es un repositorio de **paquetes** (NuGet/npm) más que de imágenes.

## Aplicación a nuestro contexto

`monolito-modular-template` ya se construye y despliega con Docker Compose (dev, staging, prod).
Con Harbor, el flujo quedaría:

1. CI construye la imagen de `Host.Api` y hace `push` a `harbor.<dominio>/<proyecto>/host-api:<sha>`
   con un robot account.
2. Harbor la escanea; si tiene CVEs críticas, la bloquea.
3. `compose-staging.yaml` / `compose.yaml` pasan de `build:` a `image: harbor.<dominio>/...:<tag>`
   → staging y producción despliegan **la misma imagen** que se probó, sin recompilar en el servidor.
4. Las imágenes base (`mcr.microsoft.com/dotnet/aspnet`, SQL Server, Seq) se consumen vía proxy
   caché para no depender de registros externos en cada despliegue.

Sigue en [07-glosario-y-fuentes.md](07-glosario-y-fuentes.md).
