# 4. Laboratorio: instalación paso a paso (Docker Compose en Linux)

Objetivo: tener Harbor funcionando en una **VM Linux** (Ubuntu 24.04 o Rocky/Alma 9) con HTTPS
autofirmado, y subir una imagen. Todos los comandos se ejecutan **en el servidor Linux**
(bash), salvo donde diga "en tu equipo".

> Reemplaza `harbor.lab.local` por tu nombre DNS real y `vX.Y.Z` por la versión que elijas en
> <https://github.com/goharbor/harbor/releases>.

## Paso 1 — Instalar Docker Engine + Compose

```bash
# Ubuntu / Debian / RHEL / Rocky: script oficial de Docker
curl -fsSL https://get.docker.com | sudo sh
sudo systemctl enable --now docker
docker --version && docker compose version
```

## Paso 2 — Descargar el instalador

```bash
VER=vX.Y.Z
cd /opt
# offline = trae todas las imágenes dentro (~600 MB+); online = las baja de Docker Hub al instalar
sudo curl -LO https://github.com/goharbor/harbor/releases/download/${VER}/harbor-offline-installer-${VER}.tgz
sudo tar xzvf harbor-offline-installer-${VER}.tgz
cd harbor
```

## Paso 3 — Certificados TLS (autofirmado para laboratorio)

En producción usa un certificado de tu CA o de Let's Encrypt y salta este paso.

```bash
DOM=harbor.lab.local
sudo mkdir -p /data/cert && cd /data/cert

# 1) CA propia
sudo openssl genrsa -out ca.key 4096
sudo openssl req -x509 -new -nodes -sha512 -days 3650 -subj "/CN=Lab Harbor CA" -key ca.key -out ca.crt

# 2) Certificado del servidor firmado por la CA (con SAN, obligatorio)
sudo openssl genrsa -out $DOM.key 4096
sudo openssl req -sha512 -new -subj "/CN=$DOM" -key $DOM.key -out $DOM.csr
cat <<EOF | sudo tee v3.ext
authorityKeyIdentifier=keyid,issuer
basicConstraints=CA:FALSE
keyUsage = digitalSignature, nonRepudiation, keyEncipherment, dataEncipherment
extendedKeyUsage = serverAuth
subjectAltName = DNS:$DOM
EOF
sudo openssl x509 -req -sha512 -days 3650 -extfile v3.ext -CA ca.crt -CAkey ca.key -CAcreateserial -in $DOM.csr -out $DOM.crt
```

## Paso 4 — Configurar `harbor.yml`

```bash
cd /opt/harbor
sudo cp harbor.yml.tmpl harbor.yml
sudo nano harbor.yml
```

Lo mínimo a editar:

```yaml
hostname: harbor.lab.local

http:
  port: 80

https:
  port: 443
  certificate: /data/cert/harbor.lab.local.crt
  private_key: /data/cert/harbor.lab.local.key

harbor_admin_password: CambiaEstaPassword!      # solo se usa en el primer arranque

database:
  password: OtraPasswordFuerte                   # password del PostgreSQL interno

data_volume: /data                               # aquí viven BD, blobs (si es filesystem), etc.

# Opcional: blobs en S3/MinIO en lugar de disco local
# storage_service:
#   s3:
#     accesskey: ...
#     secretkey: ...
#     region: us-east-1
#     bucket: harbor-blobs
#     regionendpoint: https://minio.lab.local

# Opcional: métricas Prometheus
# metric:
#   enabled: true
#   port: 9090
#   path: /metrics
```

## Paso 5 — Instalar

```bash
sudo ./install.sh --with-trivy      # --with-trivy agrega el escáner de vulnerabilidades
docker compose ps                   # todos los servicios deben estar "healthy"
```

`install.sh` corre `./prepare` (genera configs en `./common/config` y el `docker-compose.yml`) y
luego `docker compose up -d`. Si cambias `harbor.yml` después:

```bash
sudo docker compose down
sudo ./prepare --with-trivy
sudo docker compose up -d
```

## Paso 6 — Entrar a la UI

Navega a `https://harbor.lab.local` (agrega el nombre a tu DNS o a tu archivo `hosts`).
Usuario `admin` y la password del `harbor.yml`. **Cámbiala de inmediato.**

Crea un proyecto `backend` (privado) y activa en su configuración:
*Automatically scan images on push* y *Prevent vulnerable images from running* (severidad High).

## Paso 7 — Confiar en el certificado desde los clientes

Docker exige TLS válido. Con certificado autofirmado, copia la CA a cada cliente:

```bash
# Cliente Linux con Docker
sudo mkdir -p /etc/docker/certs.d/harbor.lab.local
sudo cp ca.crt /etc/docker/certs.d/harbor.lab.local/ca.crt
```

En **Windows con Docker Desktop** (tu equipo): importa `ca.crt` en *Entidades de certificación
raíz de confianza* del almacén de Windows y reinicia Docker Desktop.

> Evita `insecure-registries` (HTTP sin TLS): funciona, pero manda credenciales en claro.

## Paso 8 — Primer push y pull (en tu equipo)

```powershell
docker login harbor.lab.local
docker pull nginx:alpine
docker tag nginx:alpine harbor.lab.local/backend/nginx:lab
docker push harbor.lab.local/backend/nginx:lab
```

En la UI verás el artefacto, su digest y, tras unos segundos, el resultado del escaneo Trivy.

## Paso 9 — Arranque automático

Los contenedores tienen `restart: always`, así que regresan si Docker arranca con el sistema
(`systemctl enable docker`). Si prefieres un servicio explícito:

```ini
# /etc/systemd/system/harbor.service
[Unit]
Description=Harbor
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=/opt/harbor
ExecStart=/usr/bin/docker compose up -d
ExecStop=/usr/bin/docker compose down

[Install]
WantedBy=multi-user.target
```

## Variante: Kubernetes con Helm

```bash
helm repo add harbor https://helm.goharbor.io
helm repo update
helm install harbor harbor/harbor -n harbor --create-namespace \
  --set expose.type=ingress \
  --set expose.ingress.hosts.core=harbor.miempresa.com \
  --set externalURL=https://harbor.miempresa.com \
  --set harborAdminPassword='CambiaEsto' \
  --set persistence.persistentVolumeClaim.registry.size=200Gi
```

Para HA real: `database.type=external`, `redis.type=external`, almacenamiento de objetos
(`persistence.imageChartStorage.type=s3`/`azure`/`gcs`) y varias réplicas por componente. Ver
<https://goharbor.io/docs/main/install-config/harbor-ha-helm/>.

Sigue en [05-operacion-diaria.md](05-operacion-diaria.md).
