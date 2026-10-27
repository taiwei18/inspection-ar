#!/usr/bin/env bash
# 在服务器上执行（由 deploy.ps1 上传到 /tmp/inspection-ar-deploy 后调用），可重复执行。
# 安装或更新：后端二进制、前端静态文件、systemd 服务、nginx 站点。已有数据文件保留不动。
set -euo pipefail

SRC=/tmp/inspection-ar-deploy
APP=/opt/inspection-ar

id inspection >/dev/null 2>&1 || useradd --system --no-create-home --shell /usr/sbin/nologin inspection

mkdir -p "$APP/data" "$APP/web"
install -m 755 "$SRC/inspection-backend" "$APP/inspection-backend"
rm -rf "$APP/web.new" && mkdir -p "$APP/web.new"
tar -xzf "$SRC/web-dist.tgz" -C "$APP/web.new"
rm -rf "$APP/web" && mv "$APP/web.new" "$APP/web"
chown -R inspection:inspection "$APP/data"

install -m 644 "$SRC/inspection-ar.service" /etc/systemd/system/inspection-ar.service
systemctl daemon-reload
systemctl enable inspection-ar >/dev/null
systemctl restart inspection-ar

install -m 644 "$SRC/nginx-inspection-ar.conf" /etc/nginx/sites-available/inspection-ar
ln -sf /etc/nginx/sites-available/inspection-ar /etc/nginx/sites-enabled/inspection-ar
nginx -t
systemctl reload nginx

sleep 1
echo "service: $(systemctl is-active inspection-ar)"
curl -fsS http://127.0.0.1:8080/api/health && echo
rm -rf "$SRC"
echo "deploy ok"
