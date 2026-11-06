param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$Portable
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$PublishDir = Join-Path $RepoRoot "publish\SOPRO"
$OutputDir = Join-Path $RepoRoot "installer"
$SampleProjectName = "CATALOGO GENERAL 2026.soproproj"
$SampleProjectDir = Join-Path $RepoRoot "installer\assets\proyectos"
$SampleProject = Join-Path $SampleProjectDir $SampleProjectName
$MaxSampleProjectBytes = 2MB

function Assert-SampleProjectPreflight {
    if (-not (Test-Path -LiteralPath $SampleProject -PathType Leaf)) {
        throw "Falta el proyecto de ejemplo que se instala en Documentos\SOPRO\Proyectos: $SampleProject"
    }

    $sample = Get-Item -LiteralPath $SampleProject
    if ($sample.Length -le 0 -or $sample.Length -gt $MaxSampleProjectBytes) {
        throw "El proyecto de ejemplo debe pesar entre 1 byte y 2 MiB; tamaño actual: $($sample.Length) bytes ($SampleProject)"
    }

    $header = New-Object byte[] 16
    $stream = [System.IO.File]::OpenRead($SampleProject)
    try {
        $read = $stream.Read($header, 0, $header.Length)
    }
    finally {
        $stream.Dispose()
    }

    $sqliteMagic = [System.Text.Encoding]::ASCII.GetBytes("SQLite format 3")
    $validHeader = $read -eq 16 -and $header[15] -eq 0 -and
        [System.Text.Encoding]::ASCII.GetString($header, 0, $sqliteMagic.Length) -eq "SQLite format 3"
    if (-not $validHeader) {
        throw "El proyecto de ejemplo no tiene cabecera SQLite válida ('SQLite format 3\0'): $SampleProject"
    }

    $sidecars = Get-ChildItem -LiteralPath $SampleProjectDir -File -Force |
        Where-Object { $_.Name -like "*.soproproj-wal" -or $_.Name -like "*.soproproj-shm" -or $_.Name -like "*.lock" }
    if ($sidecars) {
        throw "El proyecto de ejemplo no debe tener archivos auxiliares WAL/SHM/lock en $SampleProjectDir : $($sidecars.Name -join ', ')"
    }
}

Assert-SampleProjectPreflight

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

if ($Portable) {
    dotnet publish (Join-Path $RepoRoot "SOPRO.WinForms\SOPRO.WinForms.csproj") `
        -c $Configuration -r $Runtime --self-contained false `
        -p:Version="$Version" -o $PublishDir
}
else {
    dotnet publish (Join-Path $RepoRoot "SOPRO.WinForms\SOPRO.WinForms.csproj") `
        -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true `
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
