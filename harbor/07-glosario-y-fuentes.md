# 7. Glosario y fuentes

## Glosario

| Término | Significado |
|---|---|
| **Artefacto** | Cualquier cosa guardada en el registro con formato OCI: imagen, chart Helm, firma, SBOM. |
| **Air‑gapped** | Red aislada, sin acceso a internet. |
| **Blob** | Archivo binario del registro (una capa o una config), identificado por su hash. |
| **CNCF** | Cloud Native Computing Foundation. "Graduado" = su nivel máximo de madurez. |
| **Cosign / Notation** | Herramientas para firmar artefactos (Sigstore / Notary v2). |
| **CVE** | Identificador público de una vulnerabilidad conocida. |
| **Digest** | Hash `sha256:…` que identifica contenido exacto e inmutable. |
| **Garbage collection (GC)** | Proceso que borra del disco las capas que ya nadie referencia. |
| **Manifest** | JSON que describe una imagen (config + lista de capas). |
| **OCI** | Open Container Initiative: estándares de formato de imagen y API de distribución. |
| **OIDC** | OpenID Connect: SSO moderno (Entra ID, Keycloak, Okta…). |
| **Proxy caché** | Proyecto de Harbor que espeja un registro externo y guarda lo que se descarga. |
| **Proyecto** | Unidad de Harbor que agrupa repositorios, miembros, cuotas y políticas. |
| **RBAC** | Control de acceso basado en roles. |
| **Replicación** | Copia de artefactos entre Harbor y otros registros. |
| **Robot account** | Cuenta técnica para automatización (CI/CD, Kubernetes). |
| **SBOM** | Software Bill of Materials: inventario de componentes de una imagen. |
| **Tag** | Nombre legible y movible de una versión (`1.4.0`, `latest`). |
| **Tag inmutable** | Tag que Harbor no permite sobrescribir ni borrar. |
| **Trivy** | Escáner open source de vulnerabilidades (de Aqua Security) que Harbor trae integrado. |

## Documentación oficial

- Sitio y docs: <https://goharbor.io/docs/>
- Requisitos de instalación: <https://goharbor.io/docs/main/install-config/installation-prereqs/>
- Instalación y configuración (`harbor.yml`): <https://goharbor.io/docs/main/install-config/>
- Configurar HTTPS: <https://goharbor.io/docs/main/install-config/configure-https/>
- HA con Helm: <https://goharbor.io/docs/main/install-config/harbor-ha-helm/>
- Guía de administración (usuarios, replicación, GC, proxy caché): <https://goharbor.io/docs/main/administration/>
- Guía de usuario (proyectos, robots, firmas, escaneo): <https://goharbor.io/docs/main/working-with-projects/>
- Upgrade: <https://goharbor.io/docs/main/administration/upgrade/>
- Releases y notas de versión: <https://github.com/goharbor/harbor/releases>
- Helm chart: <https://github.com/goharbor/harbor-helm>
- API (Swagger): `https://<tu-harbor>/devcenter-api-2.0` en tu propia instancia
- Página del proyecto en la CNCF: <https://www.cncf.io/projects/harbor/>

## Notas de verificación de este spike

- Requisitos de hardware/software y puertos: tomados de la página oficial de prerequisitos
  (consultada 2026-09-29).
- Cambios de v2.15.0 y v2.15.2: tomados de las release notes de GitHub.
- El soporte ARM64 y los saltos de versión permitidos en upgrades cambian entre versiones:
  **verificar contra la versión concreta** antes de instalar o actualizar.
