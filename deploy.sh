#!/usr/bin/env bash
# =============================================================================
# deploy.sh – Deploy Media Upload System lên Debian 12
# Chạy với quyền root: sudo bash deploy.sh
#
# Script sẽ:
#   1. Cài .NET 10, Node.js 22, PostgreSQL 16, Nginx
#   2. Build backend (.NET publish) + frontend (npm build)
#   3. Tạo DB + chạy EF migrations
#   4. Cài systemd service cho API
#   5. Cài Nginx reverse proxy
#
# Chạy lần đầu:
#   sudo bash deploy.sh
#
# Các lần sau (redeploy):
#   sudo bash deploy.sh
# =============================================================================

set -euo pipefail

# ── Cấu hình – THAY ĐỔI TRƯỚC KHI CHẠY ───────────────────────────────────────
APP_USER="mediaupload"
APP_DIR="/opt/media-upload"
API_PORT="5000"
DOMAIN=""                          # ví dụ: upload.company.com – để trống nếu dùng IP

DB_NAME="media_upload"
DB_USER="mediaupload_app"
DB_PASS=""                         # để trống → script tự sinh ngẫu nhiên

AES_KEY=""                         # để trống → script tự sinh ngẫu nhiên
ADMIN_TOKEN=""                     # để trống → script tự sinh ngẫu nhiên

FRONTEND_API_URL="http://localhost:${API_PORT}"  # URL API mà browser gọi tới
# ─────────────────────────────────────────────────────────────────────────────

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'
log()  { echo -e "${GREEN}[✓]${NC} $*"; }
warn() { echo -e "${YELLOW}[!]${NC} $*"; }
err()  { echo -e "${RED}[✗]${NC} $*"; exit 1; }

[[ $EUID -ne 0 ]] && err "Chạy script với quyền root: sudo bash deploy.sh"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="${SCRIPT_DIR}/backend"
FRONTEND_DIR="${SCRIPT_DIR}/frontend/media-upload-ui"

[[ -d "$BACKEND_DIR" ]] || err "Không tìm thấy thư mục backend/ bên cạnh script"
[[ -d "$FRONTEND_DIR" ]] || err "Không tìm thấy thư mục frontend/media-upload-ui/"

# ── Sinh giá trị ngẫu nhiên nếu chưa đặt ─────────────────────────────────────
[[ -z "$DB_PASS" ]]     && DB_PASS=$(openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 32)
[[ -z "$AES_KEY" ]]     && AES_KEY=$(openssl rand -base64 32)
[[ -z "$ADMIN_TOKEN" ]] && ADMIN_TOKEN=$(openssl rand -base64 48 | tr -dc 'A-Za-z0-9_-' | head -c 64)

# =============================================================================
# BƯỚC 1: Cài dependencies
# =============================================================================
log "Cập nhật apt..."
apt-get update -qq

# .NET 10
if ! command -v dotnet &>/dev/null || [[ "$(dotnet --version 2>/dev/null | cut -d. -f1)" -lt 10 ]]; then
    log "Cài .NET 10..."
    wget -q https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb \
        -O /tmp/packages-microsoft-prod.deb
    dpkg -i /tmp/packages-microsoft-prod.deb
    apt-get update -qq
    apt-get install -y dotnet-sdk-10.0
else
    log ".NET $(dotnet --version) đã có"
fi

# Node.js 22
if ! command -v node &>/dev/null || [[ "$(node -e 'process.stdout.write(process.version.slice(1).split(\".\")[0])')" -lt 20 ]]; then
    log "Cài Node.js 22..."
    curl -fsSL https://deb.nodesource.com/setup_22.x | bash -
    apt-get install -y nodejs
else
    log "Node.js $(node --version) đã có"
fi

# PostgreSQL 16
if ! command -v psql &>/dev/null; then
    log "Cài PostgreSQL 16..."
    apt-get install -y postgresql postgresql-client
    systemctl enable postgresql
    systemctl start postgresql
else
    log "PostgreSQL $(psql --version | awk '{print $3}') đã có"
fi

# Nginx
if ! command -v nginx &>/dev/null; then
    log "Cài Nginx..."
    apt-get install -y nginx
    systemctl enable nginx
else
    log "Nginx đã có"
fi

# =============================================================================
# BƯỚC 2: Tạo user hệ thống
# =============================================================================
if ! id "$APP_USER" &>/dev/null; then
    log "Tạo user $APP_USER..."
    useradd -r -s /bin/false -d "$APP_DIR" "$APP_USER"
fi

# =============================================================================
# BƯỚC 3: Tạo PostgreSQL database
# =============================================================================
log "Cấu hình PostgreSQL..."
sudo -u postgres psql -tc "SELECT 1 FROM pg_roles WHERE rolname='${DB_USER}'" | grep -q 1 || \
    sudo -u postgres psql -c "CREATE USER ${DB_USER} WITH PASSWORD '${DB_PASS}';"

sudo -u postgres psql -tc "SELECT 1 FROM pg_database WHERE datname='${DB_NAME}'" | grep -q 1 || \
    sudo -u postgres psql -c "CREATE DATABASE ${DB_NAME} OWNER ${DB_USER};"

sudo -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE ${DB_NAME} TO ${DB_USER};"
sudo -u postgres psql -d "${DB_NAME}" -c "GRANT ALL ON SCHEMA public TO ${DB_USER};"
log "DB: ${DB_NAME} / user: ${DB_USER}"

CONN_STRING="Host=localhost;Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASS}"

# =============================================================================
# BƯỚC 4: Build Backend
# =============================================================================
log "Build .NET backend..."
cd "$BACKEND_DIR"

dotnet restore MediaUpload.slnx -v quiet
dotnet publish MediaUpload.API/MediaUpload.API.csproj \
    -c Release \
    -o "${APP_DIR}/api" \
    --no-restore \
    -v quiet

log "Publish xong → ${APP_DIR}/api"

# =============================================================================
# BƯỚC 5: Build Frontend
# =============================================================================
log "Build React frontend..."
cd "$FRONTEND_DIR"

# Ghi .env.production với URL API thực
cat > .env.production <<EOF
VITE_API_URL=${FRONTEND_API_URL}
EOF

npm ci --silent
npm run build

# Copy dist vào app dir
mkdir -p "${APP_DIR}/ui"
cp -r dist/. "${APP_DIR}/ui/"
log "Frontend build xong → ${APP_DIR}/ui"

# =============================================================================
# BƯỚC 6: Ghi appsettings.Production.json
# =============================================================================
log "Ghi cấu hình production..."
mkdir -p "${APP_DIR}/api"

cat > "${APP_DIR}/api/appsettings.Production.json" <<EOF
{
  "ConnectionStrings": {
    "Default": "${CONN_STRING}"
  },
  "Encryption": {
    "AesKey": "${AES_KEY}"
  },
  "Cors": {
    "AllowedOrigins": ["${FRONTEND_API_URL}"]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
EOF
chmod 640 "${APP_DIR}/api/appsettings.Production.json"

# =============================================================================
# BƯỚC 7: EF Migration
# =============================================================================
log "Chạy EF database migration..."
ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Default="${CONN_STRING}" \
    Encryption__AesKey="${AES_KEY}" \
    dotnet "${APP_DIR}/api/MediaUpload.API.dll" &

API_PID=$!
sleep 8  # Chờ migrate xong rồi tắt
kill $API_PID 2>/dev/null || true
wait $API_PID 2>/dev/null || true
log "Migration hoàn tất"

# =============================================================================
# BƯỚC 8: Systemd service
# =============================================================================
log "Cài systemd service..."

cat > /etc/systemd/system/media-upload-api.service <<EOF
[Unit]
Description=Media Upload API (.NET 10)
After=network.target postgresql.service

[Service]
Type=notify
User=${APP_USER}
WorkingDirectory=${APP_DIR}/api
ExecStart=/usr/bin/dotnet ${APP_DIR}/api/MediaUpload.API.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=media-upload-api

Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:${API_PORT}
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Hardening
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=full
ReadWritePaths=${APP_DIR}/api /mnt/nas

[Install]
WantedBy=multi-user.target
EOF

# Permissions
chown -R "${APP_USER}:${APP_USER}" "${APP_DIR}"
chmod 750 "${APP_DIR}"

systemctl daemon-reload
systemctl enable media-upload-api
systemctl restart media-upload-api
log "Service media-upload-api đang chạy"

# =============================================================================
# BƯỚC 9: Nginx config
# =============================================================================
log "Cấu hình Nginx..."

SERVER_NAME="${DOMAIN:-_}"

cat > /etc/nginx/sites-available/media-upload <<EOF
upstream media_api {
    server 127.0.0.1:${API_PORT};
    keepalive 32;
}

server {
    listen 80;
    server_name ${SERVER_NAME};

    # Frontend SPA
    root ${APP_DIR}/ui;
    index index.html;

    # Tăng giới hạn upload (phải >= max_file_size trong system settings)
    client_max_body_size 1600m;
    client_body_timeout  300s;
    proxy_read_timeout   300s;
    proxy_send_timeout   300s;

    # Gzip
    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml;

    # Static assets – cache dài
    location ~* \.(js|css|png|svg|ico|woff2?)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
        try_files \$uri =404;
    }

    # API – proxy tới .NET
    location /api/ {
        proxy_pass         http://media_api;
        proxy_http_version 1.1;
        proxy_set_header   Host              \$host;
        proxy_set_header   X-Real-IP         \$remote_addr;
        proxy_set_header   X-Forwarded-For   \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
        proxy_set_header   Connection        "";
    }

    # SignalR WebSocket
    location /hubs/ {
        proxy_pass         http://media_api;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade    \$http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host       \$host;
        proxy_cache_bypass \$http_upgrade;
    }

    # Health check
    location /health {
        proxy_pass http://media_api;
    }

    # SPA fallback – React Router
    location / {
        try_files \$uri \$uri/ /index.html;
    }
}
EOF

ln -sf /etc/nginx/sites-available/media-upload /etc/nginx/sites-enabled/
rm -f /etc/nginx/sites-enabled/default

nginx -t && systemctl reload nginx
log "Nginx đã reload"

# =============================================================================
# HOÀN TẤT
# =============================================================================
PUBLIC_IP=$(curl -sf https://api.ipify.org 2>/dev/null || hostname -I | awk '{print $1}')

echo ""
echo "══════════════════════════════════════════════════════"
echo -e " ${GREEN}Deploy thành công!${NC}"
echo "══════════════════════════════════════════════════════"
echo " URL:          http://${DOMAIN:-$PUBLIC_IP}"
echo " API:          http://${DOMAIN:-$PUBLIC_IP}/api"
echo " Swagger:      http://localhost:${API_PORT}/swagger  (chỉ từ server)"
echo " Logs:         journalctl -u media-upload-api -f"
echo ""
echo " ┌─ Thông tin bảo mật (LƯU LẠI NGAY) ─────────────────"
echo " │  DB User:     ${DB_USER}"
echo " │  DB Password: ${DB_PASS}"
echo " │  AES Key:     ${AES_KEY}"
echo " │  Admin Token: ${ADMIN_TOKEN}"
echo " │  (đăng nhập UI bằng Bearer token ở trên)"
echo " └─────────────────────────────────────────────────────"
echo ""
warn "Sau khi đăng nhập: vào Config → Credentials → Rotate token admin ngay!"
echo ""

# Lưu thông tin bảo mật vào file chỉ root đọc được
SECRETS_FILE="${APP_DIR}/.deploy-secrets"
cat > "$SECRETS_FILE" <<EOF
# Generated: $(date -u '+%Y-%m-%d %H:%M:%S UTC')
DB_USER=${DB_USER}
DB_PASS=${DB_PASS}
AES_KEY=${AES_KEY}
ADMIN_TOKEN=${ADMIN_TOKEN}
CONN_STRING=${CONN_STRING}
EOF
chmod 600 "$SECRETS_FILE"
chown root:root "$SECRETS_FILE"
log "Thông tin bảo mật đã lưu tại ${SECRETS_FILE} (chỉ root đọc được)"
