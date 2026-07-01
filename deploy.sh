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
# Các lần sau (redeploy – tự động BỎ QUA các bước đã hoàn tất, xem checkpoint):
#   sudo bash deploy.sh
#
# Chạy lại từ đầu, bỏ qua checkpoint cũ:
#   sudo bash deploy.sh --reset
# =============================================================================

set -euo pipefail

# Giảm rủi ro "dotnet" bị chậm/treo ở lần chạy đầu tiên (First Time Experience,
# telemetry, xml-doc extraction) – KHÔNG liên quan tới mạng nhưng vẫn nên tắt.
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export NUGET_XMLDOC_MODE=skip

# QUAN TRỌNG: Tắt MSBuild node reuse. Nếu một lần restore/build trước đây bị
# ngắt giữa chừng (kill, mất SSH, timeout...), tiến trình "MSBuild node" nền
# (dotnet .../MSBuild.dll /nodeReuse:true) có thể vẫn còn sống (mồ côi) rất
# lâu sau đó. Lần build kế tiếp sẽ cố bắt tay qua named pipe với node mồ côi
# này để tái sử dụng – nếu node đó không phản hồi, restore/build MỚI sẽ TREO
# VÔ THỜI HẠN mà không in ra bất kỳ log nào (CPU gần như 0%). Đây là nguyên
# nhân phổ biến nhất gây treo hàng giờ, không liên quan tới mạng.
export MSBUILDDISABLENODEREUSE=1

# QUAN TRỌNG: Mặc định NuGet kiểm tra chữ ký gói qua CRL/OCSP ONLINE tới các
# máy chủ ngoài NuGet (vd: crl3.digicert.com, ocsp.digicert.com...). Nếu VPS
# chặn/drop (không reject) các kết nối này, mỗi lần kiểm tra sẽ TREO tới khi
# hết timeout TCP – có thể gây ra hiện tượng "dotnet restore" treo hàng phút
# mà KHÔNG in ra bất kỳ log nào, dù kết nối tới api.nuget.org vẫn bình thường.
# => Tắt kiểm tra revocation online là cách khắc phục tiêu chuẩn cho môi
#    trường mạng hạn chế (không ảnh hưởng tới tính toàn vẹn của gói, vẫn xác
#    thực chữ ký – chỉ bỏ qua bước kiểm tra chứng chỉ có bị thu hồi hay không).
export NUGET_CERT_REVOCATION_MODE=offline

# ── Cấu hình – THAY ĐỔI TRƯỚC KHI CHẠY ───────────────────────────────────────
APP_USER="mediaupload"
APP_DIR="/opt/media-upload"
API_PORT="5000"
DOMAIN=""                          # ví dụ: upload.company.com – để trống nếu dùng IP

DB_NAME="media_upload"
DB_USER="mediaupload_app"
DB_PASS=""                         # để trống → script tự sinh ngẫu nhiên (lần đầu)

AES_KEY=""                         # để trống → script tự sinh ngẫu nhiên (lần đầu)

RESTORE_TIMEOUT_SECS=900            # dotnet restore phải xong trong 15 phút, quá thì báo lỗi rõ ràng

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

mkdir -p "$APP_DIR"

# =============================================================================
# Checkpoint / resume – cho phép chạy lại script mà không lặp lại các bước đã
# hoàn tất (đặc biệt hữu ích khi restore/build bị treo giữa chừng).
# =============================================================================
STATE_FILE="${APP_DIR}/.deploy-state"
touch "$STATE_FILE"

if [[ "${1:-}" == "--reset" ]]; then
    warn "Xoá checkpoint – sẽ chạy lại toàn bộ các bước từ đầu."
    : > "$STATE_FILE"
fi

step_done() { grep -qxF "$1" "$STATE_FILE" 2>/dev/null; }
mark_step() { echo "$1" >> "$STATE_FILE"; }

run_step() {
    local name="$1"; shift
    if step_done "$name"; then
        warn "[checkpoint] Bỏ qua bước đã hoàn tất trước đó: ${name}"
        return 0
    fi
    local t0 t1
    t0=$(date +%s)
    log "[checkpoint] ▶ Bắt đầu bước: ${name}"
    "$@"
    t1=$(date +%s)
    mark_step "$name"
    log "[checkpoint] ✔ Hoàn tất '${name}' sau $((t1 - t0))s"
}

# ── Secrets: tái sử dụng nếu đã deploy trước đó, tránh sinh lại mật khẩu DB
#    (nếu regenerate, connection string sẽ không khớp DB đã tạo ở lần chạy trước) ─
SECRETS_FILE="${APP_DIR}/.deploy-secrets"
if [[ -f "$SECRETS_FILE" ]]; then
    log "Tìm thấy secrets từ lần deploy trước → tái sử dụng DB_PASS/AES_KEY."
    # shellcheck disable=SC1090
    source "$SECRETS_FILE"
fi

[[ -z "${DB_PASS:-}" ]] && DB_PASS=$(openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 32)
[[ -z "${AES_KEY:-}" ]] && AES_KEY=$(openssl rand -base64 32)

CONN_STRING="Host=localhost;Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASS}"

cat > "$SECRETS_FILE" <<EOF
# Cập nhật: $(date -u '+%Y-%m-%d %H:%M:%S UTC')
DB_USER=${DB_USER}
DB_PASS=${DB_PASS}
AES_KEY=${AES_KEY}
CONN_STRING=${CONN_STRING}
EOF
chmod 600 "$SECRETS_FILE"
chown root:root "$SECRETS_FILE"

# =============================================================================
# BƯỚC 1: Cài dependencies
# =============================================================================
step_install_dependencies() {
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
}
run_step "install_dependencies" step_install_dependencies

# =============================================================================
# BƯỚC 2: Tạo user hệ thống
# =============================================================================
step_create_user() {
    if ! id "$APP_USER" &>/dev/null; then
        log "Tạo user $APP_USER..."
        useradd -r -s /bin/false -d "$APP_DIR" "$APP_USER"
    fi
}
run_step "create_user" step_create_user

# =============================================================================
# BƯỚC 3: Tạo PostgreSQL database
# =============================================================================
step_setup_database() {
    log "Cấu hình PostgreSQL..."
    sudo -u postgres psql -tc "SELECT 1 FROM pg_roles WHERE rolname='${DB_USER}'" | grep -q 1 || \
        sudo -u postgres psql -c "CREATE USER ${DB_USER} WITH PASSWORD '${DB_PASS}';"

    sudo -u postgres psql -tc "SELECT 1 FROM pg_database WHERE datname='${DB_NAME}'" | grep -q 1 || \
        sudo -u postgres psql -c "CREATE DATABASE ${DB_NAME} OWNER ${DB_USER};"

    sudo -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE ${DB_NAME} TO ${DB_USER};"
    sudo -u postgres psql -d "${DB_NAME}" -c "GRANT ALL ON SCHEMA public TO ${DB_USER};"
    log "DB: ${DB_NAME} / user: ${DB_USER}"
}
run_step "setup_database" step_setup_database

# =============================================================================
# BƯỚC 4: Build Backend
# =============================================================================
step_build_backend() {
    log "Build .NET backend..."
    cd "$BACKEND_DIR"

    # Dọn mọi MSBuild/dotnet build-server node cũ còn sống (mồ côi từ lần chạy
    # trước bị ngắt giữa chừng) – tránh restore/build mới bị treo vô thời hạn
    # khi cố bắt tay với node cũ không phản hồi.
    log "Dọn dẹp MSBuild build-server cũ (nếu có)..."
    pkill -9 -f 'MSBuild.dll.*nodemode' 2>/dev/null || true
    dotnet build-server shutdown &>/dev/null || true

    # Kiểm tra nhanh kết nối tới NuGet trước khi restore. `dotnet restore` bình
    # thường chỉ mất vài chục giây; nếu treo hàng giờ thì gần như luôn là do
    # mạng/DNS/IPv6/proxy chứ KHÔNG phải do dự án – kiểm tra sớm để biết ngay.
    log "Kiểm tra kết nối tới NuGet (api.nuget.org)..."
    if ! curl -sf --max-time 10 -o /dev/null https://api.nuget.org/v3/index.json; then
        warn "Không kết nối được tới api.nuget.org trong 10s."
        warn "→ dotnet restore rất có thể sẽ bị treo lâu vì lý do MẠNG, không phải do project."
        warn "  Kiểm tra thủ công: curl -v https://api.nuget.org/v3/index.json"
        warn "  Kiểm tra DNS:      cat /etc/resolv.conf ; getent hosts api.nuget.org"
        warn "  Nghi ngờ IPv6 bị chặn/không route: thử tắt tạm để test:"
        warn "      sysctl -w net.ipv6.conf.all.disable_ipv6=1"
    fi

    # Nếu máy chỉ có IPv6 link-local (không có route ra ngoài) thì tắt hẳn
    # IPv6 để tránh mọi lookup/connect kép (dual-stack) không cần thiết khi
    # restore/publish gọi ra ngoài. An toàn vì IPv6 lúc này vốn không dùng được.
    if ! ip -6 route show default &>/dev/null; then
        warn "Không có IPv6 default route → tắt IPv6 tạm thời để tránh trễ do dual-stack."
        sysctl -w net.ipv6.conf.all.disable_ipv6=1 &>/dev/null || true
    fi

    local restore_log="/tmp/dotnet-restore-$(date +%s).log"
    log "Restore NuGet packages (timeout ${RESTORE_TIMEOUT_SECS}s, log: ${restore_log})..."
    set +e
    timeout "${RESTORE_TIMEOUT_SECS}" dotnet restore MediaUpload.slnx -v minimal -nodeReuse:false 2>&1 | tee "$restore_log"
    local restore_status=${PIPESTATUS[0]}
    set -e
    if [[ $restore_status -eq 124 ]]; then
        err "dotnet restore vượt quá ${RESTORE_TIMEOUT_SECS}s và bị hủy. KHÔNG bình thường – gần như chắc chắn do MSBuild node reuse bị treo (xem 'ps aux | grep MSBuild', kill node mồ côi) hoặc mạng/NuGet feed. Xem log: ${restore_log}"
    elif [[ $restore_status -ne 0 ]]; then
        err "dotnet restore thất bại (exit ${restore_status}). Xem log: ${restore_log}"
    fi

    dotnet publish MediaUpload.API/MediaUpload.API.csproj \
        -c Release \
        -o "${APP_DIR}/api" \
        --no-restore \
        -v quiet \
        -nodeReuse:false

    log "Publish xong → ${APP_DIR}/api"
}
run_step "build_backend" step_build_backend

# =============================================================================
# BƯỚC 5: Build Frontend
# =============================================================================
step_build_frontend() {
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
}
run_step "build_frontend" step_build_frontend

# =============================================================================
# BƯỚC 6: Ghi appsettings.Production.json
# =============================================================================
step_write_appsettings() {
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
}
run_step "write_appsettings" step_write_appsettings

# =============================================================================
# BƯỚC 7: EF Migration
# =============================================================================
step_run_migration() {
    log "Chạy EF database migration..."
    ASPNETCORE_ENVIRONMENT=Production \
        ConnectionStrings__Default="${CONN_STRING}" \
        Encryption__AesKey="${AES_KEY}" \
        dotnet "${APP_DIR}/api/MediaUpload.API.dll" &

    local api_pid=$!
    sleep 8  # Chờ migrate xong rồi tắt
    kill $api_pid 2>/dev/null || true
    wait $api_pid 2>/dev/null || true
    log "Migration hoàn tất"
}
run_step "run_migration" step_run_migration

# =============================================================================
# BƯỚC 8: Systemd service
# =============================================================================
step_setup_systemd() {
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
}
run_step "setup_systemd" step_setup_systemd

# =============================================================================
# BƯỚC 9: Nginx config
# =============================================================================
step_setup_nginx() {
    log "Cấu hình Nginx..."

    local server_name="${DOMAIN:-_}"

    cat > /etc/nginx/sites-available/media-upload <<EOF
upstream media_api {
    server 127.0.0.1:${API_PORT};
    keepalive 32;
}

server {
    listen 80;
    server_name ${server_name};

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
}
run_step "setup_nginx" step_setup_nginx

# =============================================================================
# BƯỚC 10: Health check – xác minh deploy thành công
# =============================================================================
step_health_check() {
    log "Kiểm tra health sau khi deploy..."
    sleep 2

    if systemctl is-active --quiet media-upload-api; then
        log "Service media-upload-api: active"
    else
        warn "Service media-upload-api KHÔNG active! Xem: journalctl -u media-upload-api -n 50"
    fi

    local health_url="http://localhost:${API_PORT}/health"
    if curl -sf --max-time 5 "$health_url" -o /dev/null; then
        log "Health check OK: ${health_url}"
    else
        warn "Health check thất bại: ${health_url} – kiểm tra: journalctl -u media-upload-api -f"
    fi

    if nginx -t &>/dev/null; then
        log "Nginx config hợp lệ"
    else
        warn "Nginx config có lỗi – chạy: nginx -t"
    fi
}
# Luôn chạy health check (không dùng checkpoint) vì đây chỉ là bước xác minh, không thay đổi hệ thống
step_health_check

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
echo " Checkpoint:   ${STATE_FILE}  (xoá hoặc dùng --reset để chạy lại từ đầu)"
echo ""
echo " ┌─ Thông tin bảo mật (LƯU LẠI NGAY) ─────────────────"
echo " │  DB User:       ${DB_USER}"
echo " │  DB Password:   ${DB_PASS}"
echo " │  AES Key:       ${AES_KEY}"
echo " │  Admin Token:   MediaUploadAdmin2024!ChangeMe"
echo " │  (token admin mặc định được seed sẵn trong DB – KHÔNG phải sinh ngẫu nhiên)"
echo " └─────────────────────────────────────────────────────"
echo ""
warn "Sau khi đăng nhập: vào Config → Credentials → Rotate token admin ngay!"
echo ""
log "Thông tin bảo mật đã lưu tại ${SECRETS_FILE} (chỉ root đọc được)"
