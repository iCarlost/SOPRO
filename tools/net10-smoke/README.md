# net10-smoke — piloto de arranque SOPRO
Smoke test para un runner GitHub-hosted Windows efímero. No es producto ni añade
dependencias a los proyectos de producto.
- `Invoke-Smoke.ps1`: ejecuta las pruebas.
- `Invoke-Pilot.ps1`: wrapper; falla exit inesperado, `results.json` ausente o
  resultado incoherente. Solo acepta PASS/BLOCKED.
No hay csproj rastreados: el script copia las fuentes y genera csproj efímeros
bajo `<RunDir>\generated` (RUNNER_TEMP/TEMP). La versión de EF Sqlite se lee de
`SOPRO.Data`; no se añade dependencia persistente.
## Qué prueba
Publica `SOPRO.WinForms` self-contained/single-file y arranca `Program.Main` con
una copia del fixture canónico `SOPRO.Tests/TestData/proyecto-sintetico-vial-demo.db`
(proyecto `PROYECTO SINTETICO VIAL DEMO`).
La apertura exige **ambas** señales, asociadas a ruta/PID:
1. `ConnectionOpened` + `CommandExecuted` de EF Core 10 sobre la copia exacta del
   fixture y la tabla `Proyectos`.
2. Ventana WinForms del mismo PID cuyo título contenga el nombre del proyecto.
El watchdog no termina sin ambas. `.lock` o cargar SQLite no prueban apertura.
`project-signal.txt`/`window-signal.txt` solo contienen PID, evento, ruta, tabla,
título y flags; nunca SQL general.
## Modos
`-StaticOnly` (local, sin riesgo): hook, switch, proveedor SQLite 10, observador
EF y ventana WinForms pura. No arranca SOPRO, no usa HKCU, no escribe perfil.
```powershell
pwsh ./tools/net10-smoke/Invoke-Smoke.ps1 -RepoRoot <worktree> -WorkDir $env:TEMP -StaticOnly -KeepRunDir
```
Full: solo CI; exige `GITHUB_ACTIONS=true`, `RUNNER_ENVIRONMENT=github-hosted`,
`RUNNER_OS=Windows`, `RUNNER_TEMP` y perfil limpio. Sin HKCU. Sin escritorio o
perfil limpio: `BLOCKED` antes de arrancar. Nunca en local.
## Disparadores
`.github/workflows/net10-smoke-pilot.yml`:
- `pull_request` `types: [labeled]` con condición exacta
  `github.event.label.name == 'net10-smoke-pilot'`;
- `workflow_dispatch` futuro.
No corre por push ni otras etiquetas (skipped). No comparte concurrencia con
`ci.yml`. En PR hace checkout del **HEAD** (`head.sha`, repositorio head para
forks), **no del merge**; en dispatch usa el ref seleccionado. `contents: read`,
`persist-credentials: false`, sin secretos ni write. El label lo aplica un
mantenedor; no se crea aquí. Si GitHub no dispara un workflow nuevo desde el PR
(p. ej. forks), queda pendiente; no se fuerza ni fusiona.
## Estados y evidencia
| Exit | Estado | Significado |
| --- | --- | --- |
| 0 | PASS | Todo lo ejecutado pasó. |
| 1 | FAIL | Fallo real del test/producto/arnés; no es BLOCKED. |
| 3 | BLOCKED | Runner incapaz antes de arrancar (sin escritorio/perfil limpio). |
`results.json` incluye `checks`, `processes` (PID/exit/timeout) y
`failureEvidence`. Se sube solo `results.json`, `logs/probe-*.log`,
`probes/**/hook-*.txt`, `probes/app/project-signal.txt` y
`probes/app/window-signal.txt`; sin `ef-events.log`, binarios ni logs de la app;
retención 3 días.
## Fechas
`provider` es un control de `Microsoft.Data.Sqlite` 10, no de SOPRO. Compara
`UtcTicks` pre10/modern y se etiqueta `scope=provider-only`. Cualquier diferencia
UTC es **inconclusa respecto a SOPRO**; no se usa como PASS de la app.
## Seguridad y límites
- Sin HKCU, perfil real, instalador ni secretos. Las guardas de entorno evitan
  ejecuciones accidentales; no son frontera de seguridad.
- Copia del fixture: rechaza WAL no vacío y detecta cambios durante la copia con
  `FileShare.Read` + hash/metadatos.
- Subdirectorio propio `<WorkDir>\sopro-smoke-<utc>-<guid>\` con marcador; nunca
  borra la base ni contenido ajeno. WorkDir se valida antes de crearse.
- La app se fuerza a terminar tras las señales; no valida cierre ordenado.
- Single-file deja `e_sqlite3.dll` junto al exe. El hook no soporta trimming/AOT.
- No se declara apertura real hasta un PASS con evidencias en CI.
