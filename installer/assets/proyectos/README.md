# Proyecto de ejemplo del instalador

`CATALOGO GENERAL 2026.soproproj` es la instantánea que el instalador copia a
`Documentos\SOPRO\Proyectos` solo si el archivo no existe. El instalador nunca
sobrescribe el proyecto del usuario y el desinstalador nunca lo elimina
(flags `onlyifdoesntexist uninsneveruninstall`).

- Archivo: `CATALOGO GENERAL 2026.soproproj`
- Proyecto: `CATALOGO GENERAL 2026`
- Esquema: `2026.04.4` (`SOPRO.Application/Services/SchemaManager.cs`)
- SHA-256: `B3AEE2DCAB232BDC1099CCF9D3C235153D3695B56119CABD7C5000C320A0F584`
- Origen: instantánea provista por el mantenedor para distribución pública.

## Mantenimiento

- Regenerar solo con el respaldo de SQLite (`VACUUM INTO` o backup online), de
  modo que el archivo quede autocontenido y consistente.
- El directorio no debe contener archivos `-wal`, `-shm` ni `.lock`: CI y
  `build-release.ps1` fallan si aparecen.
- Actualizar el archivo en cada incremento de versión de esquema y sincronizar
  aquí la versión y el SHA-256 (`Get-FileHash -Algorithm SHA256`).
