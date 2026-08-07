# 生成 MSIX 自签名代码签名证书并导出 pfx（用于本地测试安装）
# 用法: powershell -ExecutionPolicy Bypass -File scripts\make-cert.ps1
$ErrorActionPreference = "Stop"

$pfxPath = Join-Path $PSScriptRoot "..\src\Minesweeper\Minesweeper_TemporaryKey.pfx"
$pfxPath = [System.IO.Path]::GetFullPath($pfxPath)

# 如果证书已存在则跳过
if (Test-Path $pfxPath) {
    Write-Host "证书已存在: $pfxPath（跳过生成）"
    exit 0
}

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject "CN=Lawrence" `
    -KeyUsage DigitalSignature `
    -KeyExportPolicy Exportable `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(5)

# 导出为无密码 pfx（构建工具可直接导入）
# 注意：必须重新从证书库打开证书，否则私钥句柄未加载导致 Export 返回 null
$reopened = Get-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)"
$bytes = $reopened.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx)
[System.IO.File]::WriteAllBytes($pfxPath, $bytes)

Write-Host "证书已生成: $pfxPath"
Write-Host "指纹(Thumbprint): $($cert.Thumbprint)"
