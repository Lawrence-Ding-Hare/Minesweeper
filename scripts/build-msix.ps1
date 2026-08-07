# 一键构建 MSIX 安装包
# 注意：本机构建必须用 VS MSBuild（.NET Framework 版）+ SDK 8 副本（XamlCompiler 进程内模式需要）
# 用法: powershell -ExecutionPolicy Bypass -File scripts\build-msix.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root "src\Minesweeper\Minesweeper.csproj"

# 1. 确保签名证书存在
powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "make-cert.ps1")

# 2. 确保 SDK 8 副本存在（VS MSBuild 的 SDK 解析用）
$sdkCopy = "C:\dotnet8sdks\Sdks"
if (-not (Test-Path $sdkCopy)) {
    Write-Host "正在创建 SDK 8 副本（首次需要）..."
    Copy-Item "C:\Program Files\dotnet\sdk\8.0.423\Sdks" "C:\dotnet8sdks" -Recurse
    New-Item -ItemType Directory -Force -Path "C:\dotnet8sdks\Sdks\Microsoft.NET.SDK.WorkloadAutoImportPropsLocator\Sdk" | Out-Null
    New-Item -ItemType Directory -Force -Path "C:\dotnet8sdks\Sdks\Microsoft.NET.SDK.WorkloadManifestTargetsLocator\Sdk" | Out-Null
    Set-Content "C:\dotnet8sdks\Sdks\Microsoft.NET.SDK.WorkloadAutoImportPropsLocator\Sdk\AutoImport.props" "<Project />"
    Set-Content "C:\dotnet8sdks\Sdks\Microsoft.NET.SDK.WorkloadManifestTargetsLocator\Sdk\AutoImport.props" "<Project />"
    Set-Content "C:\dotnet8sdks\Sdks\Microsoft.NET.SDK.WorkloadManifestTargetsLocator\Sdk\WorkloadManifest.targets" "<Project />"
    Copy-Item "C:\Program Files\dotnet\sdk\8.0.423\Microsoft.NETCoreSdk.BundledVersions.props" "C:\dotnet8sdks\" -Force
    Write-Host "SDK 副本已创建"
}

# 3. 还原 + 构建（VS MSBuild + 主机参数）
$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe"
Write-Host "=== 还原 ==="
dotnet restore $proj
Write-Host "=== 构建 MSIX（VS MSBuild）==="
& $msbuild $proj /p:Configuration=Release /p:Platform=x64 /v:minimal `
    /p:BundledRuntimeIdentifierGraphFile="C:\Program Files\dotnet\sdk\8.0.423\RuntimeIdentifierGraph.json" `
    /p:NETCoreSdkRuntimeIdentifier=win-x64 `
    /p:NetCoreRoot="C:\Program Files\dotnet" `
    /p:NETCoreSdkVersion=8.0.423 `
    /p:NetCoreTargetingPackRoot="C:\Program Files\dotnet\packs"
if ($LASTEXITCODE -ne 0) { Write-Host "构建失败" -ForegroundColor Red; exit 1 }

# 4. 定位产物
$msix = Get-ChildItem (Join-Path $root "artifacts") -Recurse -Filter "*.msix" |
    Where-Object { $_.Name -notlike "*Dependencies*" } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ""
Write-Host "=== 构建完成 ===" -ForegroundColor Green
Write-Host "安装包: $($msix.FullName)"
Write-Host "安装：运行 scripts\install.ps1（会装证书 + 安装包）"
