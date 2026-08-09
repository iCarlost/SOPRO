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
$OutputDir = Join-Path $RepoRoot "installer\Output"
$Iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

Write-Host "==> Publicando SOPRO v$Version (PublishDir: $PublishDir)" -ForegroundColor Cyan

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

if (-not (Test-Path -LiteralPath $Iscc)) {
    throw "Inno Setup no está instalado en $Iscc. Descárgalo en https://jrsoftware.org/isdl.php"
}

Write-Host "==> Generando instalador con Inno Setup" -ForegroundColor Cyan
if (Test-Path -LiteralPath $OutputDir) {
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}

& $Iscc (Join-Path $RepoRoot "installer\SOPRO-InnoSetup.iss")
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup falló con código $LASTEXITCODE"
}

Write-Host "==> Instalador generado:" -ForegroundColor Green
Get-ChildItem -LiteralPath $OutputDir -Filter "SOPRO-Setup-*.exe" | Select-Object FullName, Length
