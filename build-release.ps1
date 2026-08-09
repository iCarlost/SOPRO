param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$PublishDir = Join-Path $RepoRoot "publish\SOPRO"
$OutputDir = Join-Path $RepoRoot "installer"
$IsccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)
$Iscc = $IsccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $Iscc) {
    throw "Inno Setup no está instalado. Descárgalo en https://jrsoftware.org/isdl.php"
}

Write-Host "==> Publicando SOPRO v$Version (PublishDir: $PublishDir)" -ForegroundColor Cyan

if (Test-Path -LiteralPath $PublishDir) {
    Remove-Item -LiteralPath $PublishDir -Recurse -Force
}

if ($SelfContained) {
    dotnet publish (Join-Path $RepoRoot "SOPRO.WinForms\SOPRO.WinForms.csproj") `
        -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true `
        -p:Version="$Version" -o $PublishDir
}
else {
    dotnet publish (Join-Path $RepoRoot "SOPRO.WinForms\SOPRO.WinForms.csproj") `
        -c $Configuration -r $Runtime --self-contained false `
        -p:Version="$Version" -o $PublishDir
}

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish falló con código $LASTEXITCODE"
}

$exe = Join-Path $PublishDir "SOPRO.WinForms.exe"
$fileVersion = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
if ($fileVersion -notlike "$Version*") {
    throw "Versión publicada ($fileVersion) no coincide con $Version"
}

Write-Host "==> Generando instalador con Inno Setup" -ForegroundColor Cyan

& $Iscc (Join-Path $RepoRoot "installer\SOPRO-InnoSetup.iss")
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup falló con código $LASTEXITCODE"
}

Write-Host "==> Instalador generado:" -ForegroundColor Green
Get-ChildItem -LiteralPath $OutputDir -Filter "SOPRO-Setup-$Version.exe" | Select-Object FullName, @{N = "MB"; E = { [math]::Round($_.Length / 1MB, 2) } }
