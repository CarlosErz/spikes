# 5. Operación diaria

## Integración con CI/CD (robot accounts)

No uses tu usuario personal en pipelines. Crea un **robot account** en el proyecto
(*Proyecto → Robot Accounts*), con permiso solo de `push`/`pull` sobre los repos que necesite y
una expiración. Te da un nombre tipo `robot$backend+ci` y un secreto.

```yaml
# GitHub Actions (ejemplo)
- uses: docker/login-action@v3
  with:
    registry: harbor.miempresa.com
    username: ${{ secrets.HARBOR_USER }}     # robot$backend+ci
    password: ${{ secrets.HARBOR_TOKEN }}
- run: |
    docker build -t harbor.miempresa.com/backend/api-identity:${{ github.sha }} .
    docker push harbor.miempresa.com/backend/api-identity:${{ github.sha }}
```

Para Kubernetes, crea un `imagePullSecret` con un robot de solo `pull`:

```bash
kubectl create secret docker-registry harbor-pull \
  --docker-server=harbor.miempresa.com \
  --docker-username='robot$backend+pull' --docker-password='<secreto>'
```

## Firmar imágenes (Cosign)

```bash
cosign generate-key-pair
cosign sign --key cosign.key harbor.miempresa.com/backend/api-identity@sha256:<digest>
```

Harbor muestra la firma junto al artefacto. Con *Enforce content trust* activado en el proyecto,
se bloquea el pull de imágenes no firmadas.

## Proxy caché de Docker Hub

1. *Administración → Registries → New Endpoint* tipo **Docker Hub** (idealmente con una cuenta
   para mayor límite).
2. *Projects → New Project* con **Proxy Cache** activado, apuntando a ese endpoint
   (ej. proyecto `dockerhub`).
3. Consumir: `docker pull harbor.miempresa.com/dockerhub/library/postgres:17`.

En Dockerfiles: `FROM harbor.miempresa.com/dockerhub/library/node:24-alpine`.

## Replicación

*Administración → Replications → New Rule*: origen/destino, modo **push** o **pull**, filtros por
nombre/tag/etiqueta y disparador (**manual, programado o por evento**). Usos típicos: DR entre dos
datacenters, llevar imágenes aprobadas a un Harbor de producción, sincronizar con ACR/ECR.

## Limpieza: retención + garbage collection

1. **Retención** (*Proyecto → Policy → Tag Retention*): define qué tags se conservan (ej. los 15
   más recientes y todo lo que coincida con `v*`). Programa su ejecución.
2. **Tag immutability** (*Proyecto → Policy*): protege tags de release (`v*`) contra sobrescritura.
3. **Garbage collection** (*Administración → Clean Up*): borra físicamente las capas sin
   referencias. Programarlo semanal en horario valle. Borrar un tag **no** libera disco hasta que
   corre el GC.

## Backups

Hay que respaldar **tres cosas**:

| Qué | Dónde (Compose, `data_volume: /data`) |
|---|---|
| Base de datos (metadatos) | `docker exec harbor-db pg_dumpall -U postgres > harbor-db.sql` |
| Blobs | `/data/registry` (o el bucket S3/Blob, con versionado/replicación del proveedor) |
| Configuración y secretos | `/opt/harbor/harbor.yml`, `/data/secret`, `/data/cert` |

Lo más simple y consistente: `docker compose down`, respaldar `/data` y `/opt/harbor`,
`docker compose up -d`. En Kubernetes se usa **Velero** o los snapshots del proveedor.
**Prueba la restauración** al menos una vez.

## Actualizar versión

1. Leer las notas de versión (cambios de BD, componentes eliminados).
2. **Backup completo** (ver arriba).
3. `docker compose down`, mover la carpeta vieja a `harbor.old`.
4. Descomprimir el nuevo instalador y migrar la config:
   `docker run -it --rm -v /:/hostfs goharbor/prepare:vX.Y.Z migrate -i /opt/harbor/harbor.yml`
5. `./install.sh --with-trivy`.

Harbor soporta saltar versiones con restricciones (normalmente hasta ~2 minors): revisa la guía de
upgrade de la versión destino. Ejemplo: v2.15.2 actualiza PostgreSQL 15 → 18 en el arranque.

## Monitoreo

- Habilitar `metric` y raspar con Prometheus; hay dashboards de Grafana comunitarios.
- Alertar: disco de `/data` > 80 %, contenedores no `healthy`, fallos de jobs de replicación/GC,
  expiración del certificado TLS.
- Logs en `/var/log/harbor/` del host.

## Troubleshooting frecuente

| Síntoma | Causa típica | Solución |
|---|---|---|
| `x509: certificate signed by unknown authority` | El cliente no confía en la CA | Copiar `ca.crt` a `/etc/docker/certs.d/<host>/` o al almacén del SO |
| `x509: certificate is not valid for any names` | Certificado sin SAN | Regenerar con `subjectAltName` |
| `unauthorized: unauthorized to access repository` | Rol insuficiente o proyecto equivocado | Revisar miembro/rol o permisos del robot |
| `denied: ... vulnerability` / `not signed` | Política del proyecto bloquea el pull | Corregir la imagen o ajustar la política |
| `quota exceeded` | Cuota del proyecto llena | Retención + GC, o subir cuota |
| El disco no baja tras borrar tags | Falta correr el GC | *Clean Up → GC Now* |
| Servicios `unhealthy` tras reiniciar | Docker arrancó antes que el storage/NFS, o disco lleno | `docker compose logs <servicio>`, revisar `/data` |
| Trivy no escanea en air‑gapped | Sin base de CVEs | Configurar `skip_update` y cargar la BD manualmente / mirror |

Sigue en [06-beneficios-pros-contras.md](06-beneficios-pros-contras.md).
