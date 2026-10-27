# 本机构建并部署到服务器：Go 交叉编译为 Linux 二进制、构建前端，上传后在服务器执行 install.sh。
# 前提：本机 ssh 能用密钥免密登录服务器（ssh root@117.72.117.250 不需要输入密码）。
# 用法（在仓库根目录）：pwsh deploy/deploy.ps1   或指定服务器：pwsh deploy/deploy.ps1 -Server root@1.2.3.4
param(
    [string] $Server = "root@117.72.117.250"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path ([IO.Path]::GetTempPath()) "inspection-ar-deploy"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory $out | Out-Null

Write-Host "==> 构建后端（linux/amd64）"
Push-Location (Join-Path $root "backend")
$env:GOOS = "linux"; $env:GOARCH = "amd64"; $env:CGO_ENABLED = "0"
try { go build -trimpath -ldflags "-s -w" -o (Join-Path $out "inspection-backend") . }
finally { Remove-Item Env:GOOS, Env:GOARCH, Env:CGO_ENABLED; Pop-Location }
if ($LASTEXITCODE -ne 0) { throw "go build 失败" }

Write-Host "==> 构建前端"
Push-Location (Join-Path $root "web")
try { npm run build; if ($LASTEXITCODE -ne 0) { throw "npm run build 失败" } }
finally { Pop-Location }
tar -czf (Join-Path $out "web-dist.tgz") -C (Join-Path $root "web/dist") .

Write-Host "==> 准备部署文件"
foreach ($f in "inspection-ar.service", "nginx-inspection-ar.conf", "install.sh") {
    # 统一为 LF 换行，避免在 Linux 上执行出错
    $text = [IO.File]::ReadAllText((Join-Path $PSScriptRoot $f)) -replace "`r`n", "`n"
    [IO.File]::WriteAllText((Join-Path $out $f), $text, (New-Object System.Text.UTF8Encoding($false)))
}

Write-Host "==> 上传到 $Server"
ssh -o BatchMode=yes $Server "rm -rf /tmp/inspection-ar-deploy && mkdir -p /tmp/inspection-ar-deploy"
$files = Get-ChildItem $out -File | ForEach-Object FullName # Windows 的 scp 不展开通配符，逐个列出
scp -o BatchMode=yes -q $files "${Server}:/tmp/inspection-ar-deploy/"
if ($LASTEXITCODE -ne 0) { throw "上传失败" }

Write-Host "==> 在服务器上安装"
ssh -o BatchMode=yes $Server "bash /tmp/inspection-ar-deploy/install.sh"
if ($LASTEXITCODE -ne 0) { throw "服务器安装失败" }

Remove-Item $out -Recurse -Force
$host_ = $Server.Split("@")[-1]
Write-Host "==> 完成：管理端 http://$host_/  ，手机 App 后端地址填 http://$host_"
