# 安装扫雷 MSIX（装证书到受信任根 + Add-AppxPackage）
# 用法: powershell -ExecutionPolicy Bypass -File scripts\install.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

# 1. 找到最新构建的 MSIX
$msix = Get-ChildItem (Join-Path $root "artifacts") -Recurse -Filter "*.msix" |
    Where-Object { $_.Name -notlike "*Dependencies*" } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msix) {
    Write-Host "未找到 MSIX，请先运行 scripts\build-msix.ps1" -ForegroundColor Red
    exit 1
}

# 2. 安装签名证书到"受信任的根证书颁发机构"（CurrentUser）
Write-Host "=== 安装签名证书到受信任根 ==="
$pfx = Join-Path $root "src\Minesweeper\Minesweeper_TemporaryKey.pfx"
if (Test-Path $pfx) {
    $pwd = ConvertTo-SecureString "Minesweeper2016" -Force -AsPlainText
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($pfx, $pwd)
    $cerPath = Join-Path $env:TEMP "ms_cert.cer"
    [System.IO.File]::WriteAllBytes($cerPath, $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))
    certutil -user -addstore Root $cerPath | Out-Null
    Write-Host "证书已安装到受信任根"
} else {
    Write-Host "警告：未找到证书文件，直接尝试安装"
}

# 3. 安装 MSIX
Write-Host "=== 安装 $($msix.Name) ==="
Add-AppxPackage -Path $msix.FullName
Write-Host "安装成功！请从开始菜单启动" -ForegroundColor Green
Write-Host "包名: Minesweeper.Minefield"
