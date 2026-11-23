<#
.SYNOPSIS
Smoke test del publish self-contained/single-file de SOPRO (#160).
.DESCRIPTION
Diseñado SOLO para un runner GitHub-hosted Windows efímero y dedicado. No usa
HKCU ni el perfil de un desarrollador. El modo completo solo se ejecuta desde
un workflow manual; nunca en local. -StaticOnly ejecuta únicamente pruebas
puras bajo un subdirectorio temporal propio, sin arrancar SOPRO ni tocar el
perfil.
Cada ejecución crea `<WorkDir>\sopro-smoke-<utc>-<guid>\`; solo se elimina ese
subdirectorio propio (con marcador) si la ejecución termina PASS y no se pide
-KeepRunDir. El directorio base nunca se borra.
Estados:
- PASS (0): todo lo ejecutado pasó.
- FAIL (1): fallo real del test/producto o del arnés.
- BLOCKED (3): incapacidad del runner antes de arrancar la app (sin escritorio,
  perfil no limpio). No se usa para regresiones ambiguas.
.PARAMETER RepoRoot
Raíz del repositorio SOPRO.
.PARAMETER WorkDir
Directorio base temporal. Debe estar bajo TEMP/RUNNER_TEMP.
.PARAMETER StaticOnly
Solo pruebas puras (permitido en local; no arranca SOPRO).
.PARAMETER KeepRunDir
Conserva el subdirectorio propio de la ejecución.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Get-Location).Path,
    [string]$WorkDir = $(if ($env:RUNNER_TEMP) { Join-Path $env:RUNNER_TEMP 'sopro-net10-smoke' } else { Join-Path ([IO.Path]::GetTempPath()) 'sopro-net10-smoke' }),
    [switch]$StaticOnly,
    [switch]$KeepRunDir,
    [int]$TimeoutSeconds = 180,
    [int]$WatchdogSeconds = 90,
    [int]$GraceSeconds = 5
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$script:Checks = New-Object System.Collections.Generic.List[object]
$script:Processes = New-Object System.Collections.Generic.List[object]
$script:FailureEvidence = New-Object System.Collections.Generic.List[string]
$script:Blocked = $false
$script:BlockedReason = ''
$script:SdkVersion = ''
$script:RunDir = ''
$script:BaseDir = ''
$script:AppSkippedReason = ''
function Write-Step([string]$Text) {
    Write-Host ''
    Write-Host "=== $Text ===" -ForegroundColor Cyan
}
function Add-Check([string]$Name, [bool]$Ok, [string]$Detail = '') {
    $entry = [pscustomobject]@{ name = $Name; ok = $Ok; detail = $Detail }
    $script:Checks.Add($entry)
    if ($Ok) {
        Write-Host "[PASS] $Name" -ForegroundColor Green
    }
    else {
        Write-Host "[FAIL] $Name :: $Detail" -ForegroundColor Red
    }
}
function Set-Blocked([string]$Reason) {
    $script:Blocked = $true
    $script:BlockedReason = $Reason
    Write-Host "[BLOCKED] $Reason" -ForegroundColor Yellow
}
function Add-ProcessEvidence(
    [string]$Role,
    [string]$ProcessId,
    [int]$ExitCode,
    [bool]$TimedOut,
    [string]$ProbeDir) {
    $script:Processes.Add([pscustomobject]@{
        role = $Role
        pid = $ProcessId
        exitCode = $ExitCode
        timedOut = $TimedOut
        probeDir = $ProbeDir
    })
}
function Add-FailureEvidence([string]$Path) {
    if (-not [string]::IsNullOrWhiteSpace($Path) -and
        -not $script:FailureEvidence.Contains($Path)) {
        $script:FailureEvidence.Add($Path)
    }
}
function Invoke-Dotnet([string[]]$Arguments, [string]$LogPath) {
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        Push-Location -LiteralPath $RepoRoot
        try { & dotnet @Arguments *> $LogPath }
        finally { Pop-Location }
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previous
    }
}
function Invoke-External([string]$FilePath, [string[]]$Arguments, [string]$LogPath) {
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $FilePath @Arguments *> $LogPath
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previous
    }
}
function Read-KeyValues([string]$Path) {
    $values = @{}
    if (-not (Test-Path -LiteralPath $Path)) {
        return $values
    }
    foreach ($line in [IO.File]::ReadAllLines($Path, [Text.Encoding]::UTF8)) {
        $separator = $line.IndexOf('=')
        if ($separator -gt 0) {
            $values[$line.Substring(0, $separator)] = $line.Substring($separator + 1)
        }
    }
    return $values
}
function Read-EfEvents([string]$Path) {
    $events = New-Object System.Collections.Generic.List[object]
    if (-not (Test-Path -LiteralPath $Path)) {
        return $events
    }
    foreach ($line in [IO.File]::ReadAllLines($Path, [Text.Encoding]::UTF8)) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        $values = @{}
        foreach ($pair in $line.Split('&')) {
            $separator = $pair.IndexOf('=')
            if ($separator -gt 0) {
                $values[$pair.Substring(0, $separator)] =
                    [Uri]::UnescapeDataString($pair.Substring($separator + 1))
            }
        }
        if ($values.Count -gt 0) {
            $events.Add([pscustomobject]@{
                pid = $values['pid']
                utc = $values['utc']
                event = $values['event']
                dataSource = $values['dataSource']
                table = $values['table']
                titlePresent = $values['titlePresent']
                title = $values['title']
                sql = $values['sql']
            })
        }
    }
    return $events
}
function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}
function Assert-FixtureWalClean([string]$Path) {
    $wal = $Path + '-wal'
    if (Test-Path -LiteralPath $wal) {
        $length = (Get-Item -LiteralPath $wal).Length
        if ($length -gt 0) {
            throw "WAL no vacío ($length bytes): $wal. No se copia un fixture con WAL pendiente."
        }
    }
}
function Copy-CanonicalFixtureSnapshot(
    [string]$Source,
    [string]$Destination,
    [string]$Label) {
    Assert-FixtureWalClean $Source
    $before = Get-Item -LiteralPath $Source
    $beforeHash = Get-Sha256 $Source
    $destinationDirectory = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $destinationDirectory)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }
    # FileShare.Read impide escrituras/borrados mientras dura la copia. Si un
    # escritor SQLite ya tiene el archivo abierto, la apertura falla y no se
    # genera una copia potencialmente inconsistente.
    $sourceStream = [IO.File]::Open(
        $Source,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::Read)
    try {
        $destinationStream = [IO.File]::Open(
            $Destination,
            [IO.FileMode]::Create,
            [IO.FileAccess]::Write,
            [IO.FileShare]::None)
        try {
            $sourceStream.CopyTo($destinationStream, 131072)
        }
        finally {
            $destinationStream.Dispose()
        }
    }
    finally {
        $sourceStream.Dispose()
    }
    Assert-FixtureWalClean $Source
    $after = Get-Item -LiteralPath $Source
    $afterHash = Get-Sha256 $Source
    $destinationHash = Get-Sha256 $Destination
    if ($before.Length -ne $after.Length -or
        $before.LastWriteTimeUtc -ne $after.LastWriteTimeUtc -or
        $beforeHash -ne $afterHash) {
        throw "El fixture cambió durante la copia ($Label): antes=$beforeHash después=$afterHash."
    }
    if ($destinationHash -ne $afterHash) {
        throw "La copia del fixture no coincide ($Label): copia=$destinationHash origen=$afterHash."
    }
    Add-Check "fixture-copy-$Label" $true "sha256=$destinationHash"
}
function Normalize-Path([string]$Path) {
    try {
        return [IO.Path]::GetFullPath($Path)
    }
    catch {
        return $Path
    }
}
function Assert-PathUnder([string]$Path, [string]$Root) {
    if ([string]::IsNullOrWhiteSpace($Path) -or [string]::IsNullOrWhiteSpace($Root)) {
        return $false
    }
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    return $fullPath.Equals($fullRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($fullRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}
function Get-InteractiveStatus {
    $userInteractive = [Environment]::UserInteractive
    $sessionId = (Get-Process -Id $PID).SessionId
    return [pscustomobject]@{
        userInteractive = $userInteractive
        sessionId = $sessionId
        ok = $userInteractive -and $sessionId -ne 0
    }
}
function New-GeneratedProject(
    [string]$ProjectDir,
    [string]$ProjectName,
    [string]$SourcePath,
    [string]$TargetFramework,
    [string]$OutputType,
    [string]$AssemblyName,
    [bool]$WinForms,
    [string]$PackageVersion) {
    New-Item -ItemType Directory -Path $ProjectDir -Force | Out-Null
    $sourceFile = Split-Path -Leaf $SourcePath
    Copy-Item -LiteralPath $SourcePath -Destination (Join-Path $ProjectDir $sourceFile) -Force
    $win = if ($WinForms) { '<UseWindowsForms>true</UseWindowsForms>' } else { '' }
    $package = if ($PackageVersion) {
        '<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="' + $PackageVersion + '" />'
    } else { '' }
    $xml = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>$OutputType</OutputType>
    <TargetFramework>$TargetFramework</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AssemblyName>$AssemblyName</AssemblyName>
    $win
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$sourceFile" />
    $package
  </ItemGroup>
</Project>
"@
    $projectPath = Join-Path $ProjectDir ($ProjectName + '.csproj')
    [IO.File]::WriteAllText($projectPath, $xml, (New-Object Text.UTF8Encoding($false)))
    return $projectPath
}
function New-OwnedRunDir([string]$BaseDir) {
    $runName = 'sopro-smoke-' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '-' + [Guid]::NewGuid().ToString('N')
    $runDir = Join-Path $BaseDir $runName
    if (Test-Path -LiteralPath $runDir) {
        throw "Colisión inesperada de directorio de ejecución: $runDir"
    }
    New-Item -ItemType Directory -Path $runDir | Out-Null
    $marker = Join-Path $runDir '.sopro-smoke-owned'
    $content = @(
        "runId=$runName",
        "baseDir=$BaseDir",
        "createdUtc=$([DateTime]::UtcNow.ToString('O'))",
        "scriptSha256=$((Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash)",
        "pid=$PID"
    ) -join "`n"
    [IO.File]::WriteAllText($marker, $content, (New-Object Text.UTF8Encoding($false)))
    return $runDir
}
function Remove-OwnedRunDir([string]$RunDir, [string]$BaseDir) {
    if ([string]::IsNullOrWhiteSpace($RunDir) -or [string]::IsNullOrWhiteSpace($BaseDir)) {
        return $false
    }
    $marker = Join-Path $RunDir '.sopro-smoke-owned'
    $leaf = Split-Path -Leaf $RunDir
    if (-not (Test-Path -LiteralPath $marker)) {
        return $false
    }
    if (-not $leaf.StartsWith('sopro-smoke-', [StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }
    if (-not (Assert-PathUnder $RunDir $BaseDir)) {
        return $false
    }
    Remove-Item -LiteralPath $RunDir -Recurse -Force
    return $true
}
# ---------------------------------------------------------------------------
# Validación de rutas ANTES de crear nada
# ---------------------------------------------------------------------------
if ($env:OS -ne 'Windows_NT') {
    throw 'Este smoke test solo es válido en Windows (WinForms + SQLite nativo win-x64).'
}
$RepoRoot = [IO.Path]::GetFullPath($RepoRoot)
$script:BaseDir = [IO.Path]::GetFullPath($WorkDir)
$driveRoot = [IO.Path]::GetPathRoot($script:BaseDir).TrimEnd('\', '/')
if ([string]::Equals($script:BaseDir.TrimEnd('\', '/'), $driveRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "WorkDir no puede ser la raíz de una unidad: $($script:BaseDir)"
}
$tempRoots = @([IO.Path]::GetTempPath())
if ($env:RUNNER_TEMP) {
    $tempRoots += $env:RUNNER_TEMP
}
$underAllowedTemp = $false
foreach ($root in $tempRoots) {
    if (Assert-PathUnder $script:BaseDir $root) {
        $underAllowedTemp = $true
        break
    }
}
if (-not $underAllowedTemp) {
    throw "WorkDir debe estar bajo TEMP o RUNNER_TEMP; recibido: $($script:BaseDir)"
}
if ((Assert-PathUnder $script:BaseDir $RepoRoot) -or (Assert-PathUnder $RepoRoot $script:BaseDir)) {
    throw "WorkDir no puede estar dentro del repositorio ni contenerlo: $($script:BaseDir)"
}
if (Test-Path -LiteralPath $script:BaseDir) {
    $baseItem = Get-Item -LiteralPath $script:BaseDir -Force
    if (-not $baseItem.PSIsContainer) {
        throw "WorkDir existe y no es un directorio: $($script:BaseDir)"
    }
    if (($baseItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "WorkDir no puede ser un punto de reanálisis/junction: $($script:BaseDir)"
    }
}
$staticMode = [bool]$StaticOnly
if (-not $staticMode) {
    if (-not ($env:GITHUB_ACTIONS -eq 'true')) {
        throw 'Modo completo solo para GitHub Actions. Use -StaticOnly para pruebas puras locales.'
    }
    if (-not ($env:RUNNER_ENVIRONMENT -eq 'github-hosted')) {
        throw "Modo completo solo para runner GitHub-hosted efímero; RUNNER_ENVIRONMENT='$($env:RUNNER_ENVIRONMENT)'."
    }
    if (-not ($env:RUNNER_OS -eq 'Windows')) {
        throw "Modo completo solo en Windows; RUNNER_OS='$($env:RUNNER_OS)'."
    }
    if (-not $env:RUNNER_TEMP) {
        throw 'Falta RUNNER_TEMP en el runner de GitHub.'
    }
    if (-not (Assert-PathUnder $script:BaseDir $env:RUNNER_TEMP)) {
        throw "En modo completo WorkDir debe estar bajo RUNNER_TEMP: $($script:BaseDir)"
    }
}
$solution = Join-Path $RepoRoot 'SOPRO.sln'
$winFormsProject = Join-Path $RepoRoot 'SOPRO.WinForms\SOPRO.WinForms.csproj'
$programFile = Join-Path $RepoRoot 'SOPRO.WinForms\Program.cs'
$dataProject = Join-Path $RepoRoot 'SOPRO.Data\SOPRO.Data.csproj'
$hookSource = Join-Path $PSScriptRoot 'src\StartupProbe\StartupHook.cs'
$hostSource = Join-Path $PSScriptRoot 'src\ControlProbe\ControlProbe.cs'
foreach ($required in @(
    $solution, $winFormsProject, $programFile, $dataProject, $hookSource, $hostSource
)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "No se encontró un archivo requerido antes de crear el directorio de ejecución: $required"
    }
}
if (-not (Test-Path -LiteralPath $script:BaseDir)) {
    New-Item -ItemType Directory -Path $script:BaseDir -Force | Out-Null
}
$script:RunDir = New-OwnedRunDir $script:BaseDir
$logDir = Join-Path $script:RunDir 'logs'
$probeRoot = Join-Path $script:RunDir 'probes'
$artifactsDir = Join-Path $script:RunDir 'artifacts'
$cwdDir = Join-Path $script:RunDir 'cwd'
$srcRoot = Join-Path $PSScriptRoot 'src'
foreach ($dir in @($logDir, $probeRoot, $cwdDir)) {
    New-Item -ItemType Directory -Path $dir | Out-Null
}
$gitBefore = @()
Push-Location -LiteralPath $RepoRoot
try {
    $gitBefore = @(& git status --porcelain 2>$null)
    $script:SdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
}
finally {
    Pop-Location
}
Write-Step "Preflight (RunDir: $($script:RunDir))"
Add-Check 'sdk-10.0.400' ($script:SdkVersion -match '^10\.0\.4\d{2}$') "dotnet --version=$($script:SdkVersion)"
$programText = [IO.File]::ReadAllText($programFile)
$switchIndex = $programText.IndexOf('"Microsoft.Data.Sqlite.Pre10TimeZoneHandling"')
$initializeIndex = $programText.IndexOf('ApplicationConfiguration.Initialize()')
$switchBeforeInit = $switchIndex -ge 0 -and $initializeIndex -ge 0 -and $switchIndex -lt $initializeIndex
$mainSignature = $programText -match '\[STAThread\]' -and $programText -match 'static\s+void\s+Main\s*\(\s*string\[\]\s+args\s*\)'
Add-Check 'program-switch-before-winforms-init' $switchBeforeInit `
    "switchIndex=$switchIndex initializeIndex=$initializeIndex"
Add-Check 'program-main-real' $mainSignature 'Main(string[] args) con [STAThread]'
$winFormsText = [IO.File]::ReadAllText($winFormsProject)
$dataText = [IO.File]::ReadAllText($dataProject)
$targetOk = $winFormsText -match '<TargetFramework>net10\.0-windows</TargetFramework>'
$trimOk = $winFormsText -notmatch '<PublishTrimmed>\s*true' -and $winFormsText -notmatch '<PublishAot>\s*true'
$sqlite10 = $dataText -match 'Microsoft\.EntityFrameworkCore\.Sqlite"\s+Version="10\.'
$sqlitePackageVersionMatch = [regex]::Match(
    $dataText, 'Microsoft\.EntityFrameworkCore\.Sqlite"\s+Version="([^"]+)"')
if (-not $sqlitePackageVersionMatch.Success) {
    throw 'No se pudo leer la versión de Microsoft.EntityFrameworkCore.Sqlite del producto.'
}
$sqlitePackageVersion = $sqlitePackageVersionMatch.Groups[1].Value
Add-Check 'project-net10-no-trim' ($targetOk -and $trimOk) "targetOk=$targetOk trimDisabled=$trimOk"
Add-Check 'sqlite-package-10' $sqlite10 'Microsoft.EntityFrameworkCore.Sqlite 10.x'
$versionMatch = [regex]::Match($winFormsText, '<Version>([^<]+)</Version>')
if (-not $versionMatch.Success) {
    throw 'No se pudo leer <Version> de SOPRO.WinForms.csproj.'
}
$productVersion = $versionMatch.Groups[1].Value.Trim()
# ---------------------------------------------------------------------------
# Fixture canónico (único permitido)
# ---------------------------------------------------------------------------
Write-Step 'Fixture canónico sintético'
$fixturePath = Join-Path $RepoRoot 'SOPRO.Tests\TestData\proyecto-sintetico-vial-demo.db'
$expectedProjectName = 'PROYECTO SINTETICO VIAL DEMO'
$expectedFechaInicio = '2026-04-30 14:33:07.534'
if (-not (Test-Path -LiteralPath $fixturePath)) {
    throw "No se encontró el fixture canónico: $fixturePath"
}
$fixtureHash = Get-Sha256 $fixturePath
# ---------------------------------------------------------------------------
# Build de hook y host de sondas (solo bajo RunDir)
# ---------------------------------------------------------------------------
Write-Step 'Generación efímera de proyectos y build'
$generatedDir = Join-Path $script:RunDir 'generated'
$hookProject = New-GeneratedProject `
    (Join-Path $generatedDir 'StartupProbe') 'StartupProbe' $hookSource `
    'net10.0' 'Library' 'SoproSmoke.StartupHook' $false ''
$hostProject = New-GeneratedProject `
    (Join-Path $generatedDir 'ControlProbe') 'ControlProbe' $hostSource `
    'net10.0-windows' 'Exe' 'SoproSmoke.Host' $true $sqlitePackageVersion
$hookOut = Join-Path $script:RunDir 'hook'
$hostOut = Join-Path $script:RunDir 'host'
$hookLog = Join-Path $logDir 'build-hook.log'
$hookExit = Invoke-Dotnet @(
    'publish', $hookProject, '-c', 'Release', '-p:DebugType=None',
    '-o', $hookOut, '--artifacts-path', (Join-Path $artifactsDir 'hook'), '--nologo'
) $hookLog
$hookDll = Join-Path $hookOut 'SoproSmoke.StartupHook.dll'
Add-Check 'build-hook' ($hookExit -eq 0 -and (Test-Path -LiteralPath $hookDll)) `
    "exit=$hookExit dll=$(Test-Path -LiteralPath $hookDll) log=$hookLog"
$hostLog = Join-Path $logDir 'build-host.log'
$hostExit = Invoke-Dotnet @(
    'publish', $hostProject, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '-p:PublishSingleFile=true', '-p:DebugType=None',
    '-o', $hostOut, '--artifacts-path', (Join-Path $artifactsDir 'host'), '--nologo'
) $hostLog
$hostExe = Join-Path $hostOut 'SoproSmoke.Host.exe'
Add-Check 'build-host' ($hostExit -eq 0 -and (Test-Path -LiteralPath $hostExe)) `
    "exit=$hostExit exe=$(Test-Path -LiteralPath $hostExe) log=$hostLog"
if (-not (Test-Path -LiteralPath $hookDll)) {
    throw "No se generó el hook: $hookDll"
}
if (-not (Test-Path -LiteralPath $hostExe)) {
    throw "No se generó el host de sondas: $hostExe"
}
# ---------------------------------------------------------------------------
# Pruebas puras (sin SOPRO, sin HKCU, sin perfil)
# ---------------------------------------------------------------------------
Write-Step 'Pruebas puras: hook'
$env:DOTNET_STARTUP_HOOKS = $hookDll
try {
    $switchProbe = Join-Path $probeRoot 'switch'
    New-Item -ItemType Directory -Path $switchProbe | Out-Null
    $env:SOPRO_SMOKE_PROBE_DIR = $switchProbe
    $env:SOPRO_SMOKE_WATCHDOG_SECONDS = '0'
    $switchLog = Join-Path $logDir 'probe-switch.log'
    $switchExit = Invoke-External $hostExe @('switch-probe') $switchLog
    $switchStart = Read-KeyValues (Join-Path $switchProbe 'hook-startup.txt')
    $switchEnd = Read-KeyValues (Join-Path $switchProbe 'hook-exit.txt')
    Add-ProcessEvidence 'switch' $switchStart['pid'] $switchExit $false $switchProbe
    Add-Check 'hook-observes-switch-from-main' `
        ($switchExit -eq 0 -and $switchStart['switchEnabled'] -eq 'false' -and $switchEnd['switchEnabled'] -eq 'true') `
        "exit=$switchExit startup=$($switchStart['switchEnabled']) exit=$($switchEnd['switchEnabled'])"
    Add-Check 'hook-start-before-sqlite' `
        ($switchStart['sqliteManagedLoaded'] -eq 'false' -and $switchStart['sqliteNativeLoaded'] -eq 'false') `
        "managed=$($switchStart['sqliteManagedLoaded']) native=$($switchStart['sqliteNativeLoaded'])"
}
finally {
    Remove-Item Env:DOTNET_STARTUP_HOOKS -ErrorAction SilentlyContinue
    Remove-Item Env:SOPRO_SMOKE_PROBE_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:SOPRO_SMOKE_WATCHDOG_SECONDS -ErrorAction SilentlyContinue
    Remove-Item Env:SOPRO_SMOKE_GRACE_SECONDS -ErrorAction SilentlyContinue
}
Write-Step 'Pruebas negativas: WAL, copia y propiedad'
$fixtureCopy = Join-Path $script:RunDir 'fixture-static.soproproj'
Copy-CanonicalFixtureSnapshot $fixturePath $fixtureCopy 'static'
$walProbe = Join-Path $script:RunDir 'wal-negative.db'
[IO.File]::WriteAllText($walProbe, 'x')
[IO.File]::WriteAllText($walProbe + '-wal', 'x')
$walError = ''
try { Assert-FixtureWalClean $walProbe } catch { $walError = $_.Exception.Message }
$walRejected = $walError -like '*WAL no vacío*' -and $walError -like '*No se copia un fixture*'
Add-Check 'fixture-wal-rejection' $walRejected "error=$walError"
$notOwned = Join-Path $script:RunDir 'sopro-smoke-fake'
New-Item -ItemType Directory -Path $notOwned | Out-Null
$cleanupRefused = -not (Remove-OwnedRunDir $notOwned $script:RunDir)
$notOwnedExists = Test-Path -LiteralPath $notOwned
if ($notOwnedExists) { Remove-Item -LiteralPath $notOwned -Recurse -Force }
Add-Check 'cleanup-requires-marker' ($cleanupRefused -and $notOwnedExists) `
    "refused=$cleanupRefused exists=$notOwnedExists"
$env:DOTNET_STARTUP_HOOKS = $hookDll
try {
    Write-Step 'Pruebas puras: observador EF Core (ruta/PID/SQL)'
    $efProbeData = Join-Path $script:RunDir 'efprobe.sqlite'
    $efObserverProbe = Join-Path $probeRoot 'ef-observer'
    New-Item -ItemType Directory -Path $efObserverProbe | Out-Null
    $env:SOPRO_SMOKE_PROBE_DIR = $efObserverProbe
    $env:SOPRO_SMOKE_EXPECT_DB = $efProbeData
    $env:SOPRO_SMOKE_EXPECT_TABLE = 'Entidades'
    $env:SOPRO_SMOKE_WATCHDOG_SECONDS = '60'
    $env:SOPRO_SMOKE_GRACE_SECONDS = '1'
    $efRunLog = Join-Path $logDir 'probe-ef.log'
    $efRunExit = Invoke-External $hostExe @('ef', '--db', $efProbeData, '--hold', '30') $efRunLog
    $efStart = Read-KeyValues (Join-Path $efObserverProbe 'hook-startup.txt')
    $efReady = Read-KeyValues (Join-Path $efObserverProbe 'hook-ready.txt')
    $efPid = $efStart['pid']
    $efFullPath = Normalize-Path $efProbeData
    Add-ProcessEvidence 'ef' $efPid $efRunExit $false $efObserverProbe
    $efSignalPath = Join-Path $efObserverProbe 'project-signal.txt'
    $efSignal = @(Read-EfEvents $efSignalPath)
    $efSignalConnection = @($efSignal | Where-Object {
        $_.event -eq 'Microsoft.EntityFrameworkCore.Database.Connection.ConnectionOpened' -and
        $_.pid -eq $efPid -and
        [string]::Equals((Normalize-Path $_.dataSource), $efFullPath, [StringComparison]::OrdinalIgnoreCase)
    })
    $efSignalCommand = @($efSignal | Where-Object {
        $_.event -eq 'Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted' -and
        $_.pid -eq $efPid -and
        [string]::Equals((Normalize-Path $_.dataSource), $efFullPath, [StringComparison]::OrdinalIgnoreCase) -and
        $_.table -eq 'Entidades'
    })
    $efSignalRaw = if (Test-Path -LiteralPath $efSignalPath) {
        [IO.File]::ReadAllText($efSignalPath, [Text.Encoding]::UTF8)
    } else { '' }
    $efSignalNoSql = $efSignalRaw -notmatch '(?i)\bsql\s*=' -and
        $efSignalRaw -notmatch '(?i)\b(SELECT|INSERT|UPDATE|DELETE|CREATE|PRAGMA)\b'
    $efObserverOk = $efRunExit -eq 0 -and $efSignalConnection.Count -gt 0 -and
        $efSignalCommand.Count -gt 0 -and $efSignalNoSql
    Add-Check 'ef-observer-connection-path-pid' `
        ($efRunExit -eq 0 -and $efSignalConnection.Count -gt 0) `
        "exit=$efRunExit pid=$efPid connectionOpened=$($efSignalConnection.Count)"
    Add-Check 'ef-observer-query-path-pid' `
        ($efRunExit -eq 0 -and $efSignalCommand.Count -gt 0) `
        "queryExecuted=$($efSignalCommand.Count) ruta=$efFullPath"
    Add-Check 'ef-watchdog-project-signal' `
        ($efRunExit -eq 0 -and $efReady['projectOpenSignal'] -eq 'true' -and
         (Test-Path (Join-Path $efObserverProbe 'hook-ready.txt')) -and
         -not (Test-Path (Join-Path $efObserverProbe 'hook-timeout.txt'))) `
        "exit=$efRunExit projectOpenSignal=$($efReady['projectOpenSignal']) timeout=$(Test-Path (Join-Path $efObserverProbe 'hook-timeout.txt'))"
    $efSubscribeError = Join-Path $efObserverProbe 'hook-ef-subscribe-error.txt'
    Add-Check 'ef-hook-no-subscribe-error' (-not (Test-Path -LiteralPath $efSubscribeError)) $efSubscribeError
    Add-Check 'ef-filtered-signal-no-sql' ($efObserverOk -and $efSignalRaw -match 'sqlDisclosed=false') `
        "connection=$($efSignalConnection.Count) command=$($efSignalCommand.Count) noSql=$efSignalNoSql archivo=$efSignalPath"
    if (-not $efObserverOk -or (Test-Path -LiteralPath $efSubscribeError)) {
        Add-FailureEvidence $efRunLog
        Add-FailureEvidence $efSubscribeError
        Add-FailureEvidence (Join-Path $efObserverProbe 'hook-startup.txt')
        Add-FailureEvidence (Join-Path $efObserverProbe 'hook-timeout.txt')
        Add-FailureEvidence $efSignalPath
    }
    Write-Step 'Prueba negativa: INSERT no debe generar señal'
    $insertDb = Join-Path $script:RunDir 'efprobe-insert.sqlite'
    $insertProbe = Join-Path $probeRoot 'ef-insert-negative'
    New-Item -ItemType Directory -Path $insertProbe | Out-Null
    $env:SOPRO_SMOKE_PROBE_DIR = $insertProbe
    $env:SOPRO_SMOKE_EXPECT_DB = $insertDb
    $env:SOPRO_SMOKE_EXPECT_TABLE = 'Entidades'
    $env:SOPRO_SMOKE_WATCHDOG_SECONDS = '5'
    $env:SOPRO_SMOKE_GRACE_SECONDS = '1'
    $insertLog = Join-Path $logDir 'probe-ef-insert-negative.log'
    $insertExit = Invoke-External $hostExe @('insert-only', '--db', $insertDb) $insertLog
    $insertLogText = if (Test-Path -LiteralPath $insertLog) {
        [IO.File]::ReadAllText($insertLog, [Text.Encoding]::UTF8)
    } else { '' }
    $insertCompleted = $insertLogText -match '(?m)^INSERTONLY=OK\s*$'
    $insertStart = Read-KeyValues (Join-Path $insertProbe 'hook-startup.txt')
    $insertTimeout = Read-KeyValues (Join-Path $insertProbe 'hook-timeout.txt')
    $insertSignal = @(Read-EfEvents (Join-Path $insertProbe 'project-signal.txt'))
    $insertCommand = @($insertSignal | Where-Object {
        $_.event -eq 'Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted' -and
        $_.pid -eq $insertStart['pid'] -and $_.table -eq 'Entidades'
    })
    Add-ProcessEvidence 'ef-insert-negative' $insertStart['pid'] $insertExit $true $insertProbe
    Add-Check 'ef-insert-does-not-signal' `
        ($insertExit -eq 86 -and $insertCompleted -and
         (Test-Path (Join-Path $insertProbe 'hook-timeout.txt')) -and
         -not (Test-Path (Join-Path $insertProbe 'hook-ready.txt')) -and
         $insertTimeout['projectOpenSignal'] -eq 'false' -and
         $insertCommand.Count -eq 0) `
        "exit=$insertExit completed=$insertCompleted timeout=$(Test-Path (Join-Path $insertProbe 'hook-timeout.txt')) ready=$(Test-Path (Join-Path $insertProbe 'hook-ready.txt')) command=$($insertCommand.Count)"
    if ($insertCommand.Count -gt 0 -or $insertExit -ne 86 -or -not $insertCompleted) {
        Add-FailureEvidence $insertLog
        Add-FailureEvidence (Join-Path $insertProbe 'project-signal.txt')
        Add-FailureEvidence (Join-Path $insertProbe 'hook-timeout.txt')
    }
}
finally {
    Remove-Item Env:DOTNET_STARTUP_HOOKS -ErrorAction SilentlyContinue
    Remove-Item Env:SOPRO_SMOKE_PROBE_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:SOPRO_SMOKE_EXPECT_DB -ErrorAction SilentlyContinue
    Remove-Item Env:SOPRO_SMOKE_EXPECT_TABLE -ErrorAction SilentlyContinue
    Remove-Item Env:SOPRO_SMOKE_WATCHDOG_SECONDS -ErrorAction SilentlyContinue
    Remove-Item Env:SOPRO_SMOKE_GRACE_SECONDS -ErrorAction SilentlyContinue
}
Write-Step 'Pruebas puras: ventana WinForms por PID'
$uiInteractive = Get-InteractiveStatus
Add-Check 'static-interactive-desktop' $uiInteractive.ok `
    "UserInteractive=$($uiInteractive.userInteractive) SessionId=$($uiInteractive.sessionId)"
if (-not $uiInteractive.ok) {
    Set-Blocked 'No hay escritorio interactivo; no se puede validar la ventana WinForms sin asumirlo.'
}
else {
    $uiTitle = 'SOPRO UI PROBE ' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $uiRun = Join-Path $probeRoot 'ui'
    New-Item -ItemType Directory -Path $uiRun | Out-Null
    $env:DOTNET_STARTUP_HOOKS = $hookDll
    try {
        $env:SOPRO_SMOKE_PROBE_DIR = $uiRun
        $env:SOPRO_SMOKE_EXPECT_TITLE = $uiTitle
        $env:SOPRO_SMOKE_UI_TITLE = $uiTitle
        $env:SOPRO_SMOKE_WATCHDOG_SECONDS = '30'
        $env:SOPRO_SMOKE_GRACE_SECONDS = '1'
        $uiProcess = Start-Process -FilePath $hostExe -ArgumentList 'ui' -PassThru
        $uiExited = $uiProcess.WaitForExit(60000)
        $uiExitCode = if ($uiExited) { $uiProcess.ExitCode } else { try { $uiProcess.Kill() } catch { }; -1 }
        $uiStart = Read-KeyValues (Join-Path $uiRun 'hook-startup.txt')
        $uiSignal = @(Read-EfEvents (Join-Path $uiRun 'window-signal.txt'))
        $uiSignalOk = @($uiSignal | Where-Object {
            $_.pid -eq $uiStart['pid'] -and $_.titlePresent -eq 'true'
        })
        Add-ProcessEvidence 'ui' $uiStart['pid'] $uiExitCode (-not $uiExited) $uiRun
        Add-Check 'ui-probe-title' `
            ($uiExitCode -eq 0 -and (Test-Path (Join-Path $uiRun 'hook-ready.txt')) -and
             -not (Test-Path (Join-Path $uiRun 'hook-timeout.txt')) -and $uiSignalOk.Count -gt 0) `
            "exit=$uiExitCode title=$uiTitle present=$($uiSignalOk.Count) timeout=$(Test-Path (Join-Path $uiRun 'hook-timeout.txt'))"
    }
    finally {
        Remove-Item Env:DOTNET_STARTUP_HOOKS -ErrorAction SilentlyContinue
        Remove-Item Env:SOPRO_SMOKE_PROBE_DIR -ErrorAction SilentlyContinue
        Remove-Item Env:SOPRO_SMOKE_EXPECT_TITLE -ErrorAction SilentlyContinue
        Remove-Item Env:SOPRO_SMOKE_UI_TITLE -ErrorAction SilentlyContinue
        Remove-Item Env:SOPRO_SMOKE_WATCHDOG_SECONDS -ErrorAction SilentlyContinue
        Remove-Item Env:SOPRO_SMOKE_GRACE_SECONDS -ErrorAction SilentlyContinue
    }
}
# ---------------------------------------------------------------------------
# Fase completa: solo GitHub-hosted, perfil efímero, sin HKCU
# ---------------------------------------------------------------------------
if ($staticMode) {
    Write-Step 'StaticOnly: no se publica ni arranca SOPRO'
}
else {
    Write-Step 'Comprobación de escritorio interactivo'
    $interactive = Get-InteractiveStatus
    Add-Check 'interactive-desktop' $interactive.ok `
        "UserInteractive=$($interactive.userInteractive) SessionId=$($interactive.sessionId)"
    if (-not $interactive.ok) {
        Set-Blocked 'El runner no expone un escritorio interactivo; WinForms no se puede validar sin asumirlo.'
    }
    $profileLocal = [Environment]::GetFolderPath('LocalApplicationData')
    $profileDocuments = [Environment]::GetFolderPath('MyDocuments')
    $profileLocalSopro = Join-Path $profileLocal 'SOPRO'
    $profileDocsSopro = Join-Path $profileDocuments 'SOPRO'
    Write-Step 'Comprobación de perfil de runner limpio'
    $profileFresh = -not (Test-Path -LiteralPath $profileLocalSopro) -and
        -not (Test-Path -LiteralPath $profileDocsSopro)
    Add-Check 'runner-profile-fresh' $profileFresh `
        "local=$profileLocalSopro exists=$(Test-Path $profileLocalSopro) docs=$profileDocsSopro exists=$(Test-Path $profileDocsSopro)"
    if (-not $profileFresh) {
        Set-Blocked 'El perfil ya contiene carpetas SOPRO; el script no sobrescribe ni borra datos ajenos. Use un runner efímero limpio.'
    }
    $preAppFailures = @($script:Checks | Where-Object { -not $_.ok })
    if ($preAppFailures.Count -gt 0) {
        $script:AppSkippedReason = 'Fallaron checks previos; no se arranca SOPRO para no escribir en el perfil del runner.'
    }
    if (-not $script:Blocked -and [string]::IsNullOrWhiteSpace($script:AppSkippedReason)) {
        Write-Step 'Publish self-contained/single-file de SOPRO'
        $publishDir = Join-Path $script:RunDir 'publish'
        $publishLog = Join-Path $logDir 'publish-sopro.log'
        $publishExit = Invoke-Dotnet @(
            'publish', $winFormsProject, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
            '-p:PublishSingleFile=true', "-p:Version=$productVersion", '-p:DebugType=None',
            '-o', $publishDir, '--artifacts-path', (Join-Path $artifactsDir 'product'), '--nologo'
        ) $publishLog
        $appExe = Join-Path $publishDir 'SOPRO.WinForms.exe'
        $appNative = Join-Path $publishDir 'e_sqlite3.dll'
        Add-Check 'publish-succeeded' ($publishExit -eq 0 -and (Test-Path -LiteralPath $appExe)) `
            "exit=$publishExit exe=$(Test-Path -LiteralPath $appExe) log=$publishLog"
        Add-Check 'publish-output-native-sqlite' (Test-Path -LiteralPath $appNative) `
            'e_sqlite3.dll junto al exe (no se embute por defecto)'
        if (Test-Path -LiteralPath $appExe) {
            $fileVersion = (Get-Item -LiteralPath $appExe).VersionInfo.FileVersion
            Add-Check 'publish-version' ($fileVersion -like "$productVersion*") "FileVersion=$fileVersion esperado=$productVersion"
        }
        if (@($script:Checks | Where-Object { -not $_.ok }).Count -gt 0) {
            $script:AppSkippedReason = 'Falló el publish; no se arranca la app.'
            Add-FailureEvidence $publishLog
        }
    }
    if (-not $script:Blocked -and [string]::IsNullOrWhiteSpace($script:AppSkippedReason)) {
        Write-Step 'Arranque real en perfil efímero'
        # El diálogo de privacidad es modal y bloquearía el arranque. Se siembra
        # solo en el perfil del runner efímero, nunca en un perfil de usuario real.
        New-Item -ItemType Directory -Path $profileLocalSopro -Force | Out-Null
        $privacyMarker = Join-Path $profileLocalSopro 'diagnostics-privacy-notice.version'
        [IO.File]::WriteAllText($privacyMarker, '1', (New-Object Text.UTF8Encoding($false)))
        $installedFixture = Join-Path $profileDocsSopro 'Proyectos\proyecto-sintetico-vial-demo.soproproj'
        New-Item -ItemType Directory -Path (Split-Path -Parent $installedFixture) -Force | Out-Null
        Copy-CanonicalFixtureSnapshot $fixturePath $installedFixture 'installed'
        $appProbe = Join-Path $probeRoot 'app'
        New-Item -ItemType Directory -Path $appProbe | Out-Null
        $bundleDir = Join-Path $script:RunDir 'bundle'
        New-Item -ItemType Directory -Path $bundleDir | Out-Null
        $appExitCode = -1
        $appProcessId = -1
        $appTimedOut = $false
        $env:DOTNET_STARTUP_HOOKS = $hookDll
        try {
            $env:SOPRO_SMOKE_PROBE_DIR = $appProbe
            $env:SOPRO_SMOKE_EXPECT_DB = $installedFixture
            $env:SOPRO_SMOKE_EXPECT_TABLE = 'Proyectos'
            $env:SOPRO_SMOKE_EXPECT_TITLE = $expectedProjectName
            $env:SOPRO_SMOKE_WATCHDOG_SECONDS = [string]$WatchdogSeconds
            $env:SOPRO_SMOKE_GRACE_SECONDS = [string]$GraceSeconds
            $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $bundleDir
            $quotedFixture = '"' + $installedFixture + '"'
            $process = Start-Process -FilePath $appExe -ArgumentList $quotedFixture `
                -WorkingDirectory $cwdDir -PassThru
            $appProcessId = $process.Id
            $exited = $process.WaitForExit($TimeoutSeconds * 1000)
            if (-not $exited) {
                $appTimedOut = $true
                try { $process.Kill() } catch { }
                $process.WaitForExit(10000) | Out-Null
            }
            else {
                $appExitCode = $process.ExitCode
            }
        }
        finally {
            Remove-Item Env:DOTNET_STARTUP_HOOKS -ErrorAction SilentlyContinue
            Remove-Item Env:SOPRO_SMOKE_PROBE_DIR -ErrorAction SilentlyContinue
            Remove-Item Env:SOPRO_SMOKE_EXPECT_DB -ErrorAction SilentlyContinue
            Remove-Item Env:SOPRO_SMOKE_EXPECT_TABLE -ErrorAction SilentlyContinue
            Remove-Item Env:SOPRO_SMOKE_EXPECT_TITLE -ErrorAction SilentlyContinue
            Remove-Item Env:SOPRO_SMOKE_WATCHDOG_SECONDS -ErrorAction SilentlyContinue
            Remove-Item Env:SOPRO_SMOKE_GRACE_SECONDS -ErrorAction SilentlyContinue
            Remove-Item Env:DOTNET_BUNDLE_EXTRACT_BASE_DIR -ErrorAction SilentlyContinue
        }
        Add-ProcessEvidence 'app' ([string]$appProcessId) $appExitCode $appTimedOut $appProbe
        Write-Step 'Evidencias del hook y señal de proyecto abierto'
        $appStart = Read-KeyValues (Join-Path $appProbe 'hook-startup.txt')
        $appReady = Read-KeyValues (Join-Path $appProbe 'hook-ready.txt')
        $appExitSnapshot = Read-KeyValues (Join-Path $appProbe 'hook-exit.txt')
        Add-Check 'app-hook-start-clean' `
            ($appStart['switchEnabled'] -eq 'false' -and
             $appStart['sqliteManagedLoaded'] -eq 'false' -and
             $appStart['sqliteNativeLoaded'] -eq 'false') `
            "switch=$($appStart['switchEnabled']) managed=$($appStart['sqliteManagedLoaded']) native=$($appStart['sqliteNativeLoaded'])"
        Add-Check 'app-switch-enabled' `
            ($appReady['switchEnabled'] -eq 'true' -and $appExitSnapshot['switchEnabled'] -eq 'true') `
            "ready=$($appReady['switchEnabled']) exit=$($appExitSnapshot['switchEnabled'])"
        Add-Check 'app-native-sqlite-path' `
            ($appReady['sqliteNativeLoaded'] -eq 'true' -and
             (Assert-PathUnder $appReady['nativeModulePath'] (Join-Path $script:RunDir 'publish'))) `
            "module=$($appReady['nativeModulePath'])"
        $appLocalOk = Assert-PathUnder $appStart['localAppData'] $profileLocal
        $appDocsOk = Assert-PathUnder $appStart['documents'] $profileDocuments
        Add-Check 'app-profile-is-runner' ($appLocalOk -and $appDocsOk) `
            "local=$($appStart['localAppData']) docs=$($appStart['documents'])"
        Add-Check 'app-hook-no-subscribe-error' `
            (-not (Test-Path -LiteralPath (Join-Path $appProbe 'hook-ef-subscribe-error.txt'))) `
            (Join-Path $appProbe 'hook-ef-subscribe-error.txt')
        $hookPid = $appStart['pid']
        $fixtureFull = Normalize-Path $installedFixture
        $projectSignal = $appReady['projectOpenSignal'] -eq 'true'
        $appSignalPath = Join-Path $appProbe 'project-signal.txt'
        $appSignal = @(Read-EfEvents $appSignalPath)
        $appSignalConnection = @($appSignal | Where-Object {
            $_.event -eq 'Microsoft.EntityFrameworkCore.Database.Connection.ConnectionOpened' -and
            $_.pid -eq $hookPid -and $_.pid -eq [string]$appProcessId -and
            [string]::Equals((Normalize-Path $_.dataSource), $fixtureFull, [StringComparison]::OrdinalIgnoreCase)
        })
        $appSignalCommand = @($appSignal | Where-Object {
            $_.event -eq 'Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted' -and
            $_.pid -eq $hookPid -and $_.pid -eq [string]$appProcessId -and
            [string]::Equals((Normalize-Path $_.dataSource), $fixtureFull, [StringComparison]::OrdinalIgnoreCase) -and
            $_.table -eq 'Proyectos'
        })
        $appSignalRaw = if (Test-Path -LiteralPath $appSignalPath) {
            [IO.File]::ReadAllText($appSignalPath, [Text.Encoding]::UTF8)
        } else { '' }
        $appSignalNoSql = $appSignalRaw -notmatch '(?i)\bsql\s*=' -and
            $appSignalRaw -notmatch '(?i)\b(SELECT|INSERT|UPDATE|DELETE|CREATE|PRAGMA)\b'
        $appSignalOk = $projectSignal -and $appSignalConnection.Count -gt 0 -and
            $appSignalCommand.Count -gt 0 -and $appSignalNoSql -and
            $appSignalRaw -match 'sqlDisclosed=false'
        $appWindowPath = Join-Path $appProbe 'window-signal.txt'
        $appWindow = @(Read-EfEvents $appWindowPath)
        $appWindowOk = @($appWindow | Where-Object {
            $_.pid -eq $hookPid -and $_.pid -eq [string]$appProcessId -and
            $_.titlePresent -eq 'true' -and
            $_.title.IndexOf($expectedProjectName, [StringComparison]::OrdinalIgnoreCase) -ge 0
        }).Count -gt 0
        Add-Check 'app-window-signal' $appWindowOk `
            "pid=$appProcessId expectedTitle=$expectedProjectName presente=$appWindowOk archivo=$appWindowPath"
        Add-Check 'app-project-open-signal' ($appSignalOk -and $appWindowOk) `
            "pid=$appProcessId ef=$appSignalOk ventana=$appWindowOk connection=$($appSignalConnection.Count) command=$($appSignalCommand.Count) ruta=$fixtureFull"
        if (-not $appSignalOk -or -not $appWindowOk) {
            Add-FailureEvidence $appSignalPath
            Add-FailureEvidence $appWindowPath
            Add-FailureEvidence (Join-Path $appProbe 'hook-startup.txt')
            Add-FailureEvidence (Join-Path $appProbe 'hook-ready.txt')
            Add-FailureEvidence (Join-Path $appProbe 'hook-timeout.txt')
        }
        $fixtureHashAfter = Get-Sha256 $fixturePath
        Add-Check 'fixture-source-unmodified' ($fixtureHash -eq $fixtureHashAfter) `
            "before=$fixtureHash after=$fixtureHashAfter"
        $appFailureChecks = @($script:Checks | Where-Object { $_.name -like 'app-*' -and -not $_.ok })
        if ($appFailureChecks.Count -gt 0) {
            Add-FailureEvidence (Join-Path $appProbe 'hook-startup.txt')
            Add-FailureEvidence (Join-Path $appProbe 'hook-ready.txt')
            Add-FailureEvidence $appSignalPath
            Add-FailureEvidence $appWindowPath
            Add-FailureEvidence (Join-Path $appProbe 'hook-timeout.txt')
            Add-FailureEvidence (Join-Path $appProbe 'hook-ef-subscribe-error.txt')
        }
    }
    elseif (-not $script:Blocked) {
        Write-Host "Se omite la app: $($script:AppSkippedReason)" -ForegroundColor Yellow
    }
}
# ---------------------------------------------------------------------------
# Cierre
# ---------------------------------------------------------------------------
$gitAfter = @()
Push-Location -LiteralPath $RepoRoot
try {
    $gitAfter = @(& git status --porcelain 2>$null)
}
finally {
    Pop-Location
}
$gitDelta = @(Compare-Object -ReferenceObject $gitBefore -DifferenceObject $gitAfter)
Add-Check 'repo-sin-cambios' ($gitDelta.Count -eq 0) "nuevos cambios git=$($gitDelta.Count)"
$failed = @($script:Checks | Where-Object { -not $_.ok })
$blockingCheckNames = @('static-interactive-desktop', 'interactive-desktop', 'runner-profile-fresh')
$onlyBlockingFailures = $failed.Count -gt 0
foreach ($failure in $failed) {
    if ($blockingCheckNames -notcontains $failure.name) {
        $onlyBlockingFailures = $false
        break
    }
}
$status = 'PASS'
if ($failed.Count -gt 0) {
    if ($script:Blocked -and $onlyBlockingFailures) {
        $status = 'BLOCKED'
    }
    else {
        $status = 'FAIL'
    }
}
elseif ($script:Blocked) {
    $status = 'BLOCKED'
}
if ($failed.Count -gt 0) {
    foreach ($failure in $failed) {
        Add-FailureEvidence (Join-Path $logDir 'build-hook.log')
        Add-FailureEvidence (Join-Path $logDir 'build-host.log')
    }
}
$summary = [pscustomobject]@{
    timestampUtc = [DateTime]::UtcNow.ToString('O')
    status = $status
    blockedReason = $script:BlockedReason
    appLaunched = (-not $script:Blocked) -and [string]::IsNullOrWhiteSpace($script:AppSkippedReason) -and -not $staticMode
    appOpenVerified = (-not $staticMode) -and (-not $script:Blocked) -and
        [string]::IsNullOrWhiteSpace($script:AppSkippedReason) -and ($status -eq 'PASS')
    appSkippedReason = $script:AppSkippedReason
    mode = $(if ($staticMode) { 'StaticOnly' } else { 'Full' })
    repoRoot = $RepoRoot
    baseDir = $script:BaseDir
    runDir = $script:RunDir
    productVersion = $productVersion
    sdkVersion = $script:SdkVersion
    fixture = [pscustomobject]@{
        path = $fixturePath
        sha256 = $fixtureHash
        expectedProjectName = $expectedProjectName
        expectedFechaInicio = $expectedFechaInicio
    }
    dateControl = [pscustomobject]@{
        scope = 'not-measured'
        note = 'El control de fechas provider se eliminó; no se mide ni se atribuye comportamiento de fechas a SOPRO.'
    }
    passed = ($script:Checks.Count - $failed.Count)
    failed = $failed.Count
    checks = $script:Checks
    processes = $script:Processes
    failureEvidence = $script:FailureEvidence
    pending = @(
        'Ejecutar el piloto Full por pull_request [labeled] (etiqueta exacta) o workflow_dispatch en un runner GitHub-hosted Windows con escritorio interactivo.',
        'No se afirma que la apertura real haya pasado hasta que ese piloto produzca evidencia PASS.',
        'La integración CI es un paso piloto no obligatorio; no bloquea push/PR automáticamente.'
    )
}
$summaryPath = Join-Path $script:RunDir 'results.json'
[IO.File]::WriteAllText(
    $summaryPath,
    ($summary | ConvertTo-Json -Depth 8),
    (New-Object Text.UTF8Encoding($false)))
Write-Step 'Resumen'
Write-Host ("Estado: {0} ({1}/{2} checks)" -f $status, $summary.passed, $script:Checks.Count)
if ($script:Blocked) {
    Write-Host ("Bloqueo: {0}" -f $script:BlockedReason) -ForegroundColor Yellow
}
if ($script:AppSkippedReason) {
    Write-Host ("App omitida: {0}" -f $script:AppSkippedReason) -ForegroundColor Yellow
}
Write-Host "Evidencias: $($script:RunDir)"
Write-Host "Resumen JSON: $summaryPath"
if ($failed.Count -gt 0) {
    Write-Host 'Checks fallidos:' -ForegroundColor Red
    foreach ($failure in $failed) {
        Write-Host (" - {0}: {1}" -f $failure.name, $failure.detail) -ForegroundColor Red
    }
}
$keep = $KeepRunDir -or $status -ne 'PASS'
if ($keep) {
    Write-Host "Se conserva el subdirectorio propio: $($script:RunDir)" -ForegroundColor Yellow
}
else {
    $removed = Remove-OwnedRunDir $script:RunDir $script:BaseDir
    Write-Host "Subdirectorio propio eliminado: $removed (base intacta: $($script:BaseDir))"
}
if ($status -eq 'PASS') {
    exit 0
}
if ($status -eq 'BLOCKED') {
    exit 3
}
exit 1
