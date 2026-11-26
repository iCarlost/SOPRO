<#
.SYNOPSIS
Pruebas enfocadas de los guards de build-release.ps1: icono local (SOPRO.ico) e
icono real embebido en los recursos PE del ejecutable publicado.

.DESCRIPTION
No publica, no instala y no modifica el árbol del repositorio. Copia
build-release.ps1 a un repositorio falso temporal, lo dota de fixtures
sintéticos (ICO y proyecto SQLite mínimos) y ejercita sus funciones.

Incluye controles contra artefactos reales cuando están disponibles:
- apphost local con icono -> debe aceptarse;
- ejecutable instalado sin RT_GROUP_ICON (regresión del falso positivo que daba
  la antigua búsqueda de bytes dentro del bundle single-file) -> debe rechazarse;
- notepad.exe (icono ajeno) -> debe rechazarse.

Estados: PASS (0), FAIL (1). El directorio temporal solo se elimina si todo pasa;
si falla se conserva y se imprime la ruta como evidencia.

.PARAMETER RepoRoot
Raíz del repositorio SOPRO (por defecto, dos niveles sobre este script).
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

if (-not $RepoRoot) {
    $RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}

$script:Passed = 0
$script:Failed = 0
$script:Skipped = 0

function Add-Check {
    param([string]$Name, [bool]$Ok, [string]$Detail = '')
    if ($Ok) {
        $script:Passed++
        Write-Host "[PASS] $Name" -ForegroundColor Green
    }
    else {
        $script:Failed++
        Write-Host "[FAIL] $Name :: $Detail" -ForegroundColor Red
    }
}

function Add-Skip {
    param([string]$Name, [string]$Detail = '')
    $script:Skipped++
    Write-Host "[SKIP] $Name :: $Detail" -ForegroundColor Yellow
}

function Test-Case {
    param([string]$Name, [scriptblock]$Body)
    try {
        & $Body
        Add-Check -Name $Name -Ok $true
    }
    catch {
        Add-Check -Name $Name -Ok $false -Detail $_.Exception.Message
    }
}

function Assert-True {
    param([bool]$Condition, [string]$Detail = 'Condicion falsa.')
    if (-not $Condition) {
        throw $Detail
    }
}

function Assert-ThrowsLike {
    param([scriptblock]$Body, [string]$Pattern)
    try {
        & $Body
    }
    catch {
        if ($_.Exception.Message -notmatch $Pattern) {
            throw "Mensaje inesperado '$($_.Exception.Message)'; se esperaba /$Pattern/"
        }
        return
    }
    throw "Se esperaba un error /$Pattern/ y la operacion termino sin error."
}

function New-SyntheticIconImage {
    param([int]$Length = 64, [int]$Seed = 7)
    $bytes = New-Object byte[] $Length
    for ($i = 0; $i -lt $Length; $i++) {
        $bytes[$i] = [byte]((($i + 1) * $Seed) % 251)
    }
    return ,$bytes
}

function New-SyntheticIco {
    param([byte[]]$Image, [byte]$Width = 16, [byte]$Height = 16)
    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]1)
        $writer.Write($Width)
        $writer.Write($Height)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$Image.Length)
        $writer.Write([uint32]22)
        $writer.Write($Image)
    }
    finally {
        $writer.Dispose()
    }
    return ,$stream.ToArray()
}

function New-FakeRepo {
    param([string]$Root, [bool]$IncludeIcon)
    $winforms = Join-Path $Root 'SOPRO.WinForms'
    $assets = Join-Path $Root 'installer\assets\proyectos'
    New-Item -ItemType Directory -Path $winforms, $assets -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $script:SourceRepoRoot 'build-release.ps1') -Destination (Join-Path $Root 'build-release.ps1')
    if ($IncludeIcon) {
        [System.IO.File]::WriteAllBytes((Join-Path $winforms 'SOPRO.ico'), $script:ValidIco)
    }
    $sqliteHeader = [System.Text.Encoding]::ASCII.GetBytes('SQLite format 3' + [char]0)
    [System.IO.File]::WriteAllBytes((Join-Path $assets 'CATALOGO GENERAL 2026.soproproj'), $sqliteHeader)
    return (Join-Path $Root 'build-release.ps1')
}

function New-IconFrame {
    param([int]$Width, [int]$Height, [byte[]]$Data)
    return [pscustomobject]@{ Width = $Width; Height = $Height; Data = $Data }
}

$script:SourceRepoRoot = $RepoRoot
$script:TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sopro-release-guards-{0}" -f [guid]::NewGuid().ToString('N'))
$script:Image = New-SyntheticIconImage -Length 64 -Seed 7
$script:ValidIco = New-SyntheticIco -Image $script:Image
$script:ValidIcoPath = Join-Path $script:TempRoot 'valido.ico'
$script:RealIconPath = Join-Path $script:SourceRepoRoot 'SOPRO.WinForms\SOPRO.ico'

$exitCode = 1
try {
    New-Item -ItemType Directory -Path $script:TempRoot -Force | Out-Null
    [System.IO.File]::WriteAllBytes($script:ValidIcoPath, $script:ValidIco)

    Write-Host ''
    Write-Host '=== Cargando guards desde un repositorio falso ===' -ForegroundColor Cyan
    $validScript = New-FakeRepo -Root (Join-Path $script:TempRoot 'valid') -IncludeIcon $true
    $preflightError = ''
    try {
        . $validScript -Version '0.0.0-guard-test' -ValidateOnly
    }
    catch {
        $preflightError = $_.Exception.Message
    }
    Add-Check -Name 'Preflight: icono y proyecto de ejemplo validos pasan -ValidateOnly' -Ok ($preflightError -eq '') -Detail $preflightError
    if ($preflightError -ne '') {
        throw "Sin guards cargados no se pueden ejecutar las demas pruebas: $preflightError"
    }

    Write-Host ''
    Write-Host '=== Icono local (SOPRO.ico) ===' -ForegroundColor Cyan

    Test-Case 'Icono: archivo ausente produce error explicito' {
        Assert-ThrowsLike { Assert-AppIcon -Path (Join-Path $script:TempRoot 'no-existe.ico') } 'Falta el icono'
    }

    Test-Case 'Icono: archivo vacio se rechaza' {
        $empty = Join-Path $script:TempRoot 'vacio.ico'
        [System.IO.File]::WriteAllBytes($empty, (New-Object byte[] 0))
        Assert-ThrowsLike { Assert-AppIcon -Path $empty } 'solo tiene 0 bytes'
    }

    Test-Case 'Icono: archivo que no es ICO se rechaza' {
        $text = Join-Path $script:TempRoot 'texto.ico'
        [System.IO.File]::WriteAllText($text, 'esto no es un icono')
        Assert-ThrowsLike { Assert-AppIcon -Path $text } 'reservado=0, tipo=1'
    }

    Test-Case 'Icono: ICO truncado se rechaza' {
        $truncated = Join-Path $script:TempRoot 'truncado.ico'
        $bytes = New-Object byte[] ($script:ValidIco.Length - 5)
        [System.Array]::Copy($script:ValidIco, 0, $bytes, 0, $bytes.Length)
        [System.IO.File]::WriteAllBytes($truncated, $bytes)
        Assert-ThrowsLike { Assert-AppIcon -Path $truncated } 'truncado'
    }

    Test-Case 'Icono: ICO valido devuelve dimensiones y datos de la imagen' {
        $images = @(Assert-AppIcon -Path $script:ValidIcoPath)
        Assert-True ($images.Count -eq 1) "Se esperaba 1 imagen y hay $($images.Count)."
        Assert-True ($images[0].Width -eq 16 -and $images[0].Height -eq 16) "Dimensiones inesperadas: $($images[0].Width)x$($images[0].Height)."
        Assert-True ($images[0].Data.Length -eq $script:Image.Length) "Tamano de imagen inesperado: $($images[0].Data.Length)."
    }

    Write-Host ''
    Write-Host '=== Comparacion de conjuntos de imagenes ===' -ForegroundColor Cyan

    $frame16 = New-IconFrame -Width 16 -Height 16 -Data (New-SyntheticIconImage -Length 32 -Seed 3)
    $frame32 = New-IconFrame -Width 32 -Height 32 -Data (New-SyntheticIconImage -Length 64 -Seed 5)

    Test-Case 'Comparacion: conjuntos identicos coinciden' {
        $result = Compare-AppIconFrameSets -Expected @($frame16, $frame32) -Actual @($frame16, $frame32)
        Assert-True $result.Ok 'Se esperaba coincidencia exacta.'
    }

    Test-Case 'Comparacion: mismo tamano con bytes distintos no coincide' {
        $other = New-IconFrame -Width 16 -Height 16 -Data (New-SyntheticIconImage -Length 32 -Seed 9)
        $result = Compare-AppIconFrameSets -Expected @($frame16) -Actual @($other)
        Assert-True (-not $result.Ok) 'No debia coincidir.'
        Assert-True ($result.Missing.Count -eq 1 -and $result.Extra.Count -eq 1) "Missing=$($result.Missing.Count) Extra=$($result.Extra.Count)."
    }

    Test-Case 'Comparacion: falta una imagen esperada' {
        $result = Compare-AppIconFrameSets -Expected @($frame16, $frame32) -Actual @($frame16)
        Assert-True (-not $result.Ok) 'No debia coincidir con una imagen faltante.'
        Assert-True ($result.Missing.Count -eq 1 -and $result.Missing[0] -eq '32x32') "Missing=$($result.Missing -join ',')."
    }

    Test-Case 'Comparacion: sobra una imagen en el ejecutable' {
        $result = Compare-AppIconFrameSets -Expected @($frame16) -Actual @($frame16, $frame32)
        Assert-True (-not $result.Ok) 'No debia coincidir con una imagen de mas.'
        Assert-True ($result.Extra.Count -eq 1 -and $result.Extra[0] -eq '32x32') "Extra=$($result.Extra -join ',')."
    }

    Test-Case 'Comparacion: ejecutable sin imagenes no coincide' {
        $result = Compare-AppIconFrameSets -Expected @($frame16) -Actual @()
        Assert-True (-not $result.Ok) 'Un conjunto vacio no puede coincidir.'
    }

    Write-Host ''
    Write-Host '=== Recursos de icono del ejecutable (API Win32) ===' -ForegroundColor Cyan

    Test-Case 'Ejecutable: archivo que no es PE se rechaza' {
        $notPe = Join-Path $script:TempRoot 'no-pe.bin'
        $prefix = New-SyntheticIconImage -Length 300 -Seed 17
        $blob = New-Object byte[] ($prefix.Length + $script:Image.Length)
        [System.Array]::Copy($prefix, 0, $blob, 0, $prefix.Length)
        [System.Array]::Copy($script:Image, 0, $blob, $prefix.Length, $script:Image.Length)
        [System.IO.File]::WriteAllBytes($notPe, $blob)
        Assert-ThrowsLike { Assert-PublishedAppIcon -ExePath $notPe -IconPath $script:ValidIcoPath } 'como imagen PE'
    }

    Test-Case 'Ejecutable: PE sin icono de aplicacion se rechaza' {
        Assert-ThrowsLike { Assert-PublishedAppIcon -ExePath $env:SystemRoot\System32\where.exe -IconPath $script:ValidIcoPath } 'no tiene icono|no coincide byte a byte'
    }

    Test-Case 'Ejecutable: exe inexistente produce error explicito' {
        Assert-ThrowsLike { Assert-PublishedAppIcon -ExePath (Join-Path $script:TempRoot 'no-existe.exe') -IconPath $script:ValidIcoPath } 'No existe el ejecutable'
    }

    if ((Test-Path -LiteralPath $script:RealIconPath -PathType Leaf) -and (Test-Path -LiteralPath $env:SystemRoot\System32\notepad.exe -PathType Leaf)) {
        Test-Case 'Ejecutable: icono ajeno (notepad.exe) se rechaza' {
            Assert-ThrowsLike { Assert-PublishedAppIcon -ExePath $env:SystemRoot\System32\notepad.exe -IconPath $script:RealIconPath } 'no coincide byte a byte'
        }

        $apphosts = @(Get-ChildItem -LiteralPath (Join-Path $script:SourceRepoRoot 'SOPRO.WinForms\bin') -Recurse -Filter 'SOPRO.WinForms.exe' -ErrorAction SilentlyContinue |
                Where-Object { $_.Length -gt 100KB } |
                Sort-Object LastWriteTime -Descending)
        if ($apphosts.Count -gt 0) {
            $apphost = $apphosts[0].FullName
            Test-Case "Control positivo: el apphost local con icono coincide con SOPRO.ico ($([IO.Path]::GetFileName($apphost)))" {
                Assert-PublishedAppIcon -ExePath $apphost -IconPath $script:RealIconPath
            }
        }
        else {
            Add-Skip -Name 'Control positivo: apphost local con icono' -Detail 'No hay SOPRO.WinForms.exe bajo bin\ (clon limpio o sin build).'
        }

        $installed = Join-Path $env:LOCALAPPDATA 'Programs\SOPRO\SOPRO.WinForms.exe'
        if (Test-Path -LiteralPath $installed -PathType Leaf) {
            $installedImages = @(Get-AppIconResourceImages -ExePath $installed)
            if ($installedImages.Count -eq 0) {
                Test-Case 'Regresion: exe instalado sin RT_GROUP_ICON se rechaza pese a contener el ICO en el bundle' {
                    Assert-ThrowsLike { Assert-PublishedAppIcon -ExePath $installed -IconPath $script:RealIconPath } 'no tiene icono'
                }
            }
            else {
                Add-Skip -Name 'Regresion: exe instalado sin RT_GROUP_ICON' -Detail "La instalacion actual declara $($installedImages.Count) imagenes de icono; ya no sirve como fixture generico."
            }
        }
        else {
            Add-Skip -Name 'Regresion: exe instalado sin RT_GROUP_ICON' -Detail 'No hay instalacion local de SOPRO.'
        }
    }
    else {
        Add-Skip -Name 'Controles contra artefactos reales' -Detail 'Faltan SOPRO.ico local o notepad.exe.'
    }

    Write-Host ''
    Write-Host '=== Orden de los guards en build-release.ps1 ===' -ForegroundColor Cyan

    Test-Case 'Orden: el guard del icono corre antes de dotnet publish' {
        $text = Get-Content -LiteralPath (Join-Path $script:SourceRepoRoot 'build-release.ps1') -Raw
        $guard = $text.IndexOf('$iconImages = @(Assert-AppIcon')
        $publish = $text.IndexOf('dotnet publish')
        Assert-True ($guard -ge 0 -and $publish -gt $guard) "guard=$guard publish=$publish"
    }

    Test-Case 'Orden: la verificacion del icono embebido corre tras publicar y antes de Inno Setup' {
        $text = Get-Content -LiteralPath (Join-Path $script:SourceRepoRoot 'build-release.ps1') -Raw
        $publish = $text.IndexOf('dotnet publish')
        $embedded = $text.IndexOf('Assert-PublishedAppIcon -ExePath $exe')
        $iscc = $text.IndexOf('& $Iscc')
        Assert-True ($publish -ge 0 -and $embedded -gt $publish -and $iscc -gt $embedded) "publish=$publish embedded=$embedded iscc=$iscc"
    }

    Test-Case 'Regresion: la busqueda de bytes suelta en el ejecutable no vuelve al script' {
        $text = Get-Content -LiteralPath (Join-Path $script:SourceRepoRoot 'build-release.ps1') -Raw
        Assert-True (-not ($text -match 'Test-IconImageInFile|GetEncoding\(28591\)')) 'El script todavia contiene la busqueda de bytes insegura.'
    }

    if (Test-Path -LiteralPath $script:RealIconPath -PathType Leaf) {
        Write-Host ''
        Write-Host '=== Asset local real del repositorio ===' -ForegroundColor Cyan
        Test-Case 'Icono: el asset local SOPRO.ico del repo es valido' {
            $images = @(Assert-AppIcon -Path $script:RealIconPath)
            Assert-True ($images.Count -ge 1) 'El ICO no declara imagenes.'
        }
    }
    else {
        Add-Skip -Name 'Icono: asset local SOPRO.ico' -Detail 'No existe en este clon (asset local no versionado).'
    }

    Write-Host ''
    Write-Host ("Resumen: {0} PASS, {1} FAIL, {2} SKIP" -f $script:Passed, $script:Failed, $script:Skipped)
    if ($script:Failed -eq 0) {
        $exitCode = 0
    }
}
finally {
    if ($script:Failed -eq 0 -and (Test-Path -LiteralPath $script:TempRoot)) {
        Remove-Item -LiteralPath $script:TempRoot -Recurse -Force
    }
    elseif (Test-Path -LiteralPath $script:TempRoot) {
        Write-Host "Evidencia conservada en: $script:TempRoot" -ForegroundColor Yellow
    }
}

exit $exitCode
