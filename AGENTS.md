# AGENTS.md — instrucciones para agentes de código

Fuentes ejecutables (csproj, ci.yml, scripts) priman sobre docs. Solo lo no obvio.

## Stack y solución
- WinForms .NET: `SOPRO.WinForms`, `SOPRO.Reporting` y los tests `SOPRO.Reporting.Tests`/`SOPRO.Tests.WinForms` usan `net8.0-windows` → **build de solución completo solo en Windows**. El resto es `net8.0`. SDK fijado en `global.json` (10.0.400, `rollForward: latestPatch`): usar ese SDK, no el default de la máquina.
- `SOPRO.sln` (9 proyectos): `SOPRO.Calculation` (motor, `net8.0`, cero dependencias runtime; empaqueta como NuGet `SOPRO.Calculation`, assembly `Sopro.Calculation` — casing distinto, lo valida CI) → `SOPRO.Core` (entidades) → `SOPRO.Data` (EF Core 8 / SQLite) → `SOPRO.Application` (casos de uso) → `SOPRO.Reporting` (PDF/Excel) → `SOPRO.WinForms` (UI; entrypoint `SOPRO.WinForms/Program.cs`, acepta ruta de proyecto por arg — apertura por doble clic de `.soproproj`).
- Tests MSTest: `SOPRO.Tests` (net8.0, ~940 métodos; es el único que corre multi-OS en CI), `SOPRO.Reporting.Tests` y `SOPRO.Tests.WinForms` (net8.0-windows, solo Windows).
- `consumers/` está FUERA de la solución: valida el NuGet empaquetado contra un feed local sin ProjectReference.

## Build y test
```powershell
dotnet restore SOPRO.sln --nologo
dotnet build SOPRO.sln -c Release --no-restore --nologo
dotnet test SOPRO.sln -c Release --no-build --no-restore --nologo   # suite completa (~1.5k tests)
# Test enfocado (filter VSTest, aplica a todos los proyectos de test de la solución):
dotnet test SOPRO.sln -c Release --filter "FullyQualifiedName~NombreClase"
dotnet test SOPRO.Tests/SOPRO.Tests.csproj -c Release --filter "FullyQualifiedName~Namespace.Clase.Metodo"
```
- Orden: restore → build → test con `--no-build` (lo que hace CI). Suite completa larga; para iterar usar `--filter` o un solo csproj.
- Validación local del paquete (Gate N6 de CI, ver `consumers/README.md`):
  `dotnet pack SOPRO.Calculation/SOPRO.Calculation.csproj -c Release --output .artifacts/packages` luego `dotnet run --project consumers/Sopro.Calculation.Consumer -c Release`. CI exige versión exacta `0.1.0`, cero dependencias runtime y README+CHANGELOG dentro del `.nupkg` (`ci.yml` job `calculation-package`).

## Esquema de BD (versionado INDEPENDiente del producto)
- Versión propia `AAAA.MM.revision` en `VersionActual` de `SOPRO.Application/Services/SchemaManager.cs` (hoy `2026.04.4`). NO confundir con la versión del producto.
- Todo cambio de esquema toca 3 artefactos en orden: entidad (`SOPRO.Core/Entities/`) → mapeo (`SOPRO.Data/Context/SOPROContext.cs`) → método `M0XX_*` en `SchemaManager` + bump de `VersionActual` + registro en `EnsureCurrentSchema`. Omisión = DBs viejos fallan al abrir (`no such column`). Detalle: `SCHEMA_MANTENIMIENTO.md`.
- Proyecto = archivo SQLite `.soproproj` (alias `.db`/`.sopro` legacy se abren sin renombrar); catálogos maestros siguen en `.db`.

## Versión, instalador y release (manual, sin CI de release)
- Versión del producto (hoy `1.11.0`) duplicada a mano en 2 lugares que deben ir sincronizados: `SOPRO.WinForms/SOPRO.WinForms.csproj` (`Version`/`AssemblyVersion`/`FileVersion`) e `installer/SOPRO-InnoSetup.iss` (`#define MyAppVersion`). `build-release.ps1` solo valida la `FileVersion` del ejecutable publicado (`SOPRO.WinForms.exe`), NO el `.iss`; por eso `csproj` e `.iss` deben sincronizarse a mano.
- Semántica (CONTRIBUTING): funcional=minor, corrección=patch, estructural=major.
- Release: `.\build-release.ps1 -Version X.Y.Z` (dotnet publish self-contained single-file win-x64 + ISCC; requiere Inno Setup 6 en rutas estándar Program Files / `%LOCALAPPDATA%`). Produce `installer/SOPRO-Setup-X.Y.Z.exe` (gitignored). Publicar A MANO en el repo **separado** `iCarlost/SOPRO-Releases`: tag `vX.Y.Z` + el exe como asset — el auto-updater (`SOPRO.WinForms/Services/GitHubUpdateService.cs`) lee `releases/latest` de ese repo. Sin firma de código. Instalador per-user (`PrivilegesRequired=lowest`), registra asociación `.soproproj` en HKCU.

## CI e higiene (.github/workflows/ci.yml)
- Push/PR a `main` (que está protegido: cambios solo vía PR). NO existe job de release.
- Job `repository-hygiene` FALLA si el árbol rastreado contiene: `*.db` (excepción única: `SOPRO.Tests/TestData/proyecto-sintetico-vial-demo.db`, fixture rastreado por diseño), `installer/*.exe`, `bin/`, `obj/`, `.bob/`, `.opencode/`, `.slim/`, `publish/`, `.artifacts/`, `TestResults/`, goldens `*.Real.*`, secretos (`*.pfx`/`*.snk`/`*.key`/`*.pem`/`*.env`). Nada de esto se commitea jamás; el instalador vive solo como asset de SOPRO-Releases.
- `SOPRO.WinForms/SOPRO.ico` es asset local no versionado; csproj e `.iss` lo toleran ausente (condicionales).

## Reglas del proyecto (no defaults del lenguaje)
- Issues-first: sin Issue abierto/aprobado no se acepta PR. Nuevos proyectos, capas o inversiones de dependencia requieren ADR aprobado.
- La rama local `wip/estado-autoguardado-presupuesto` en `0918c05` es solo un snapshot histórico, no trabajo pendiente: **no fusionarla ni cherry-pickearla en bloque**. `main` es la fuente de verdad; los cambios aprobados ya se portaron selectivamente en #47, #48, #49, #51 y #53. El snapshot contiene versiones antiguas que pueden revertir esas mejoras. Si se identifica algo aún útil, comparar y portar solo ese cambio con issue aprobado. U4 (reset independiente de formato) quedó diferido/no aprobado; #49 ya cubre el reset dentro del borrado atómico.
- Un solo motor de cálculo (`SOPRO.Calculation`): no duplicar lógica, no rutas paralelas de cálculo/persistencia, no mover lógica de negocio a la UI, no romper la precisión de pantalla (screen-precision) ni la compatibilidad de cálculo.
- UI en español con la terminología existente; C# tipado explícito, `nullable` habilitado; sin comentarios superfluos. Toda lógica de cálculo exige pruebas unitarias.
