param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$Portable,
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$PublishDir = Join-Path $RepoRoot "publish\SOPRO"
$OutputDir = Join-Path $RepoRoot "installer"
$IconPath = Join-Path $RepoRoot "SOPRO.WinForms\SOPRO.ico"
$SampleProjectName = "CATALOGO GENERAL 2026.soproproj"
$SampleProjectDir = Join-Path $RepoRoot "installer\assets\proyectos"
$SampleProject = Join-Path $SampleProjectDir $SampleProjectName
$MaxSampleProjectBytes = 2MB

function Assert-AppIcon {
    <#
    .SYNOPSIS
    Valida el icono local de la aplicación y devuelve sus imágenes.
    .DESCRIPTION
    SOPRO.WinForms\SOPRO.ico es un asset local no versionado: si falta, el
    ejecutable y el instalador salen sin icono de identidad. Este guard corta el
    release ANTES de publicar. Valida cabecera ICO y que cada imagen quepa en el
    archivo.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Falta el icono de la aplicación: $Path. Es un asset local no versionado; cópialo desde una instalación o una release anterior antes de generar un instalador."
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 6) {
        throw "El icono $Path no es válido: solo tiene $($bytes.Length) bytes."
    }

    $declaredImages = [BitConverter]::ToUInt16($bytes, 4)
    if ([BitConverter]::ToUInt16($bytes, 0) -ne 0 -or [BitConverter]::ToUInt16($bytes, 2) -ne 1 -or $declaredImages -lt 1) {
        throw "El icono $Path no tiene cabecera ICO válida (reservado=0, tipo=1, al menos una imagen)."
    }
    if ($bytes.Length -lt 6 + 16 * $declaredImages) {
        throw "El icono $Path está truncado: declara $declaredImages imágenes y el archivo tiene $($bytes.Length) bytes."
    }

    $images = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $declaredImages; $i++) {
        $entry = 6 + 16 * $i
        $length = [BitConverter]::ToUInt32($bytes, $entry + 8)
        $offset = [BitConverter]::ToUInt32($bytes, $entry + 12)
        if ($length -eq 0 -or ([uint64]$offset + [uint64]$length) -gt [uint64]$bytes.Length) {
            throw "El icono $Path está truncado: la imagen $i declara $length bytes en el desplazamiento $offset y el archivo tiene $($bytes.Length) bytes."
        }

        $data = New-Object byte[] $length
        [System.Array]::Copy($bytes, [int]$offset, $data, 0, [int]$length)
        $images.Add([pscustomobject]@{
                Width  = $(if ($bytes[$entry] -eq 0) { 256 } else { [int]$bytes[$entry] })
                Height = $(if ($bytes[$entry + 1] -eq 0) { 256 } else { [int]$bytes[$entry + 1] })
                Data   = $data
            })
    }

    return $images
}

$IconProbeSource = @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public sealed class SoproIconImage
{
    public int Width;
    public int Height;
    public int BitCount;
    public byte[] Data;
}

public static class SoproPeIconReader
{
    private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
    private const uint RT_ICON = 3;
    private const uint RT_GROUP_ICON = 14;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LockResource(IntPtr hResData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EnumResourceNames(IntPtr hModule, IntPtr lpType, EnumResNameProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumResNameProc(IntPtr hModule, IntPtr lpType, IntPtr lpName, IntPtr lParam);

    private static byte[] ReadResource(IntPtr module, IntPtr name, uint type)
    {
        IntPtr info = FindResource(module, name, (IntPtr)type);
        if (info == IntPtr.Zero)
        {
            throw new InvalidOperationException("No se encontro el recurso " + type + "/" + name.ToInt64() + " (Win32 error " + Marshal.GetLastWin32Error() + ").");
        }
        uint size = SizeofResource(module, info);
        IntPtr handle = LoadResource(module, info);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("No se pudo cargar el recurso (Win32 error " + Marshal.GetLastWin32Error() + ").");
        }
        IntPtr data = LockResource(handle);
        if (data == IntPtr.Zero || size == 0)
        {
            throw new InvalidOperationException("Recurso vacio.");
        }
        byte[] buffer = new byte[size];
        Marshal.Copy(data, buffer, 0, (int)size);
        return buffer;
    }

    public static SoproIconImage[] Read(string path)
    {
        IntPtr module = LoadLibraryEx(path, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
        if (module == IntPtr.Zero)
        {
            throw new InvalidOperationException("No se pudo cargar '" + path + "' como imagen PE (Win32 error " + Marshal.GetLastWin32Error() + ").");
        }
        try
        {
            List<IntPtr> groupIds = new List<IntPtr>();
            bool enumerated = EnumResourceNames(module, (IntPtr)RT_GROUP_ICON, delegate(IntPtr h, IntPtr t, IntPtr n, IntPtr p) { groupIds.Add(n); return true; }, IntPtr.Zero);
            if (!enumerated && groupIds.Count == 0)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 1813)
                {
                    throw new InvalidOperationException("No se pudieron enumerar los recursos RT_GROUP_ICON (Win32 error " + error + ").");
                }
            }

            List<SoproIconImage> images = new List<SoproIconImage>();
            foreach (IntPtr groupId in groupIds)
            {
                byte[] group = ReadResource(module, groupId, RT_GROUP_ICON);
                if (group.Length < 6 || BitConverter.ToUInt16(group, 2) != 1)
                {
                    throw new InvalidOperationException("Grupo de iconos invalido (id " + groupId.ToInt64() + ").");
                }
                int count = BitConverter.ToUInt16(group, 4);
                if (group.Length < 6 + 14 * count)
                {
                    throw new InvalidOperationException("Grupo de iconos truncado (id " + groupId.ToInt64() + ").");
                }
                for (int i = 0; i < count; i++)
                {
                    int offset = 6 + 14 * i;
                    int width = group[offset] == 0 ? 256 : group[offset];
                    int height = group[offset + 1] == 0 ? 256 : group[offset + 1];
                    int bitCount = BitConverter.ToUInt16(group, offset + 6);
                    uint bytesInRes = BitConverter.ToUInt32(group, offset + 8);
                    ushort iconId = BitConverter.ToUInt16(group, offset + 12);

                    byte[] data = ReadResource(module, (IntPtr)iconId, RT_ICON);
                    if ((uint)data.Length != bytesInRes)
                    {
                        throw new InvalidOperationException("La imagen RT_ICON " + iconId + " declara " + bytesInRes + " bytes y tiene " + data.Length + ".");
                    }
                    SoproIconImage image = new SoproIconImage();
                    image.Width = width;
                    image.Height = height;
                    image.BitCount = bitCount;
                    image.Data = data;
                    images.Add(image);
                }
            }
            return images.ToArray();
        }
        finally
        {
            FreeLibrary(module);
        }
    }
}
'@

function Get-AppIconResourceImages {
    <#
    .SYNOPSIS
    Lee los iconos embebidos en los recursos PE (RT_GROUP_ICON/RT_ICON) de un exe.
    .DESCRIPTION
    Inspecciona el directorio de recursos del propio ejecutable con las API de
    Windows (LoadLibraryEx con LOAD_LIBRARY_AS_DATAFILE, sin ejecutarlo). NO busca
    bytes sueltos en el archivo: el .ico tambien aparece dentro del bundle
    single-file por los recursos .resx de la app, y una busqueda de bytes daria
    falsos positivos aunque el apphost tenga un icono generico.
    Devuelve una lista vacia si el PE no declara ningún RT_GROUP_ICON.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExePath
    )

    if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) {
        throw "No existe el ejecutable publicado: $ExePath"
    }

    if (-not ('SoproPeIconReader' -as [type])) {
        Add-Type -TypeDefinition $IconProbeSource -Language CSharp
    }

    try {
        $images = [SoproPeIconReader]::Read($ExePath)
    }
    catch {
        throw "No se pudieron leer los iconos de $($ExePath) como imagen PE: $($_.Exception.Message)"
    }

    return $images
}

function Compare-AppIconFrameSets {
    <#
    .SYNOPSIS
    Compara byte a byte dos conjuntos de imágenes de icono.
    .DESCRIPTION
    Empareja por dimensión y contenido exacto. Devuelve Ok, Missing (dimensiones
    esperadas sin coincidencia) y Extra (dimensiones presentes de más).
    #>
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Expected,
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Actual
    )

    $remaining = New-Object System.Collections.Generic.List[object]
    foreach ($image in $Actual) {
        $remaining.Add($image)
    }

    $missing = New-Object System.Collections.Generic.List[string]
    foreach ($frame in $Expected) {
        $matchIndex = -1
        for ($i = 0; $i -lt $remaining.Count; $i++) {
            $candidate = $remaining[$i]
            if ($candidate.Width -ne $frame.Width -or $candidate.Height -ne $frame.Height) {
                continue
            }
            if ([Convert]::ToBase64String($candidate.Data) -eq [Convert]::ToBase64String($frame.Data)) {
                $matchIndex = $i
                break
            }
        }

        if ($matchIndex -ge 0) {
            $remaining.RemoveAt($matchIndex)
        }
        else {
            $missing.Add("$($frame.Width)x$($frame.Height)")
        }
    }

    $extra = New-Object System.Collections.Generic.List[string]
    foreach ($image in $remaining) {
        $extra.Add("$($image.Width)x$($image.Height)")
    }

    return [pscustomobject]@{
        Ok      = ($Actual.Count -gt 0 -and $missing.Count -eq 0 -and $extra.Count -eq 0)
        Missing = $missing.ToArray()
        Extra   = $extra.ToArray()
    }
}

function Assert-PublishedAppIcon {
    <#
    .SYNOPSIS
    Verifica que el ejecutable publicado declare como icono de aplicación el ICO local.
    .DESCRIPTION
    Compara los recursos RT_GROUP_ICON/RT_ICON del PE con las imágenes de
    SOPRO.ico: exige al menos un grupo de iconos y coincidencia exacta de
    dimensiones y bytes. Limitación: valida el icono del ejecutable, no la
    asociación de shell ni SetupIconFile del instalador.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExePath,
        [Parameter(Mandatory = $true)]
        [string]$IconPath
    )

    if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) {
        throw "No existe el ejecutable publicado: $ExePath"
    }

    $expected = @(Assert-AppIcon -Path $IconPath)
    $actual = @(Get-AppIconResourceImages -ExePath $ExePath)
    if ($actual.Count -eq 0) {
        throw "El ejecutable publicado $ExePath no tiene icono de aplicación embebido (0 recursos RT_GROUP_ICON). Revisa que ApplicationIcon se aplique al publicar."
    }

    $comparison = Compare-AppIconFrameSets -Expected $expected -Actual $actual
    if (-not $comparison.Ok) {
        $detail = New-Object System.Collections.Generic.List[string]
        if ($comparison.Missing.Count -gt 0) {
            $detail.Add("faltan: $($comparison.Missing -join ', ')")
        }
        if ($comparison.Extra.Count -gt 0) {
            $detail.Add("sobran: $($comparison.Extra -join ', ')")
        }
        throw "El icono embebido en $ExePath no coincide byte a byte con $IconPath ($($detail -join '; '))."
    }
}

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

$iconImages = @(Assert-AppIcon -Path $IconPath)
Assert-SampleProjectPreflight

if ($ValidateOnly) {
    Write-Host "==> Preflight correcto: icono con $($iconImages.Count) imágenes y proyecto de ejemplo válidos" -ForegroundColor Green
    return
}

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
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "No se generó el ejecutable esperado: $exe"
}

$fileVersion = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
if ($fileVersion -notlike "$Version*") {
    throw "Versión publicada ($fileVersion) no coincide con $Version"
}

Write-Host "==> Verificando icono embebido en el ejecutable publicado" -ForegroundColor Cyan
Assert-PublishedAppIcon -ExePath $exe -IconPath $IconPath

Write-Host "==> Generando instalador con Inno Setup" -ForegroundColor Cyan

& $Iscc (Join-Path $RepoRoot "installer\SOPRO-InnoSetup.iss")
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup falló con código $LASTEXITCODE"
}

Write-Host "==> Instalador generado:" -ForegroundColor Green
Get-ChildItem -LiteralPath $OutputDir -Filter "SOPRO-Setup-$Version.exe" | Select-Object FullName, @{N = "MB"; E = { [math]::Round($_.Length / 1MB, 2) } }
