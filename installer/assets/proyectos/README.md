# Proyecto de ejemplo del instalador

`CATALOGO GENERAL 2026.soproproj` es la instantánea que el instalador copia a
`Documentos\SOPRO\Proyectos` solo si el archivo no existe. El instalador nunca
sobrescribe el proyecto del usuario y el desinstalador nunca lo elimina
(flags `onlyifdoesntexist uninsneveruninstall`).

- Archivo: `CATALOGO GENERAL 2026.soproproj`
- Proyecto: `CATALOGO GENERAL 2026`
- Esquema: `2026.04.5` (`SOPRO.Application/Services/SchemaManager.cs`)
- Tamaño: `1,327,104` bytes
- SHA-256: `EAB40565BE4E86616C1D96F78981924475156EE7C1EB572BB455D10E70A04E4A`
- Origen: instantánea provista por el mantenedor para distribución pública.

## Mantenimiento

- Regenerar solo con el respaldo de SQLite (`VACUUM INTO` o backup online), de
  modo que el archivo quede autocontenido y consistente.
- El directorio no debe contener archivos `-wal`, `-shm` ni `.lock`: CI y
  `build-release.ps1` fallan si aparecen.
- Actualizar el archivo en cada incremento de versión de esquema y sincronizar
  aquí la versión y el SHA-256 (`Get-FileHash -Algorithm SHA256`).
