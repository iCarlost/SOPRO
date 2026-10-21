# SOPRO

Sistema de Presupuestacion de Obra Publica.

[![CI](https://github.com/iCarlost/SOPRO/actions/workflows/ci.yml/badge.svg)](https://github.com/iCarlost/SOPRO/actions)
[![License: MIT](https://img.shields.io/badge/Licencia-MIT-blue.svg)](LICENSE)

## Descripcion

SOPRO es una aplicacion de escritorio desarrollada en C# (.NET 8) para la presupuestacion de obra publica mediante analisis de precios unitarios (APU).

El sistema integra:

- Presupuesto de obra
- Matrices de precios unitarios
- Explosion de insumos
- Programa de obra (Gantt y Curva S)
- Analisis financiero
- Reportes en PDF y Excel

## Arquitectura

```
SOPRO.Calculation  -> Motor de calculo independiente
SOPRO.Core         -> Entidades
SOPRO.Application  -> Casos de uso y contratos
SOPRO.Data         -> Persistencia (SQLite / EF Core)
SOPRO.Reporting    -> Generadores PDF y Excel
SOPRO.WinForms     -> Interfaz de usuario
```

### Reglas

- No duplicar logica ni mover logica de negocio a la UI.
- El motor de calculo debe ser unico y consumirse via la ruta canonica.
- Los proyectos nuevos o inversiones de dependencia requieren un ADR aprobado.

## Compilar

Requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows).

```powershell
dotnet restore SOPRO.sln
dotnet build SOPRO.sln -c Release
```

## Pruebas

```powershell
dotnet test SOPRO.sln -c Release
```

## Empaquetado

1. Actualizar la version en `SOPRO.WinForms.csproj` y `installer/SOPRO-InnoSetup.iss`.
2. Ejecutar el script de empaquetado (requiere [Inno Setup 6](https://jrsoftware.org/isdl.php)):

```powershell
.\build-release.ps1 -Version X.Y.Z
```

3. Crear el release en GitHub y adjuntar `SOPRO-Setup-<version>.exe`.

La aplicacion consulta actualizaciones desde `iCarlost/SOPRO-Releases`. Cada version distribuida debe publicarse ahi con su etiqueta `vX.Y.Z`.

## Persistencia

- Base de datos: SQLite, un archivo por proyecto (extension `.soproproj`; ver [Formato de proyecto](#formato-de-proyecto)).
- Esquema gestionado por `SchemaManager` (version actual de esquema: `2026.04.4`).

## Formato de proyecto

Los proyectos SOPRO se guardan en un archivo SQLite con extension **`.soproproj`** (la extension canonica de los proyectos nuevos).

- **Compatibilidad**: los proyectos antiguos en formato `.db` siguen abriéndose; `.sopro` se acepta como alias. No se renombran automaticamente los archivos existentes.
- **Apertura por doble clic**: tras instalar la aplicacion, los proyectos se pueden abrir con doble clic desde el Explorador. El instalador registra la asociacion por usuario, sin permisos de administrador; el icono mostrado es el de la aplicacion. Al desinstalar se elimina la asociacion, pero se conservan los documentos de proyecto.
- **Catalogos internos**: el formato de los catalogos (p. ej. el catalogo maestro) no cambia; siguen siendo `.db`.

## Formato de celdas en reportes

SOPRO permite personalizar el formato (fuente, tamano, negrita, cursiva, alineacion, colores, ajuste de texto y formato numerico) de columnas y de celdas individuales en los reportes, con seleccion estilo Excel desde el ribbon:

- **Columna completa**: clic en el encabezado de la columna y aplicar el formato.
- **Celdas**: seleccionar una o mas celdas; el formato aplica solo a la seleccion. Shift extiende un rango contiguo; Ctrl selecciona celdas o rangos discontinuos. El indicador del ribbon muestra "selección de N celda(s)" y avisa cuando las celdas tienen formatos distintos (estado mixto).
- **Deshacer/rehacer**: Ctrl+Z / Ctrl+Y. Un rango aplicado se deshace como UNA sola accion.

Disponible en Presupuesto, Indirectos, APU/Matrices, Explosion de Insumos, Programas de Obra/Insumos, Analisis de Financiamiento y los catalogos de Materiales, Mano de Obra, Herramientas y Maquinaria. Los estilos se guardan en el archivo del proyecto y se aplican igual en pantalla, PDF y Excel (los generadores consumen un snapshot neutral, no el grid).

Guia completa (precedencia de estilos, claves estables y migracion): [docs/formato-celdas-reportes.md](docs/formato-celdas-reportes.md).

## Versionado

```
v1.10.0
```

- Cambios funcionales: minor
- Correcciones: patch
- Cambios estructurales: major

## Contribuciones

Lee [CONTRIBUTING.md](CONTRIBUTING.md) antes de enviar un cambio. Toda contribucion comienza con un Issue.

## Seguridad

Para reportar vulnerabilidades, consulta [SECURITY.md](SECURITY.md). Utiliza el canal de **Private Vulnerability Reporting** de GitHub.

## Soporte

Consulta [SUPPORT.md](SUPPORT.md). El soporte es best-effort sobre la ultima version estable.

## Licencia

SOPRO se distribuye bajo la licencia **MIT**. Ver [LICENSE](LICENSE).

La licencia MIT aplica al codigo fuente y documentacion tecnica del repositorio. **No cubre** el nombre comercial "SOPRO", logotipos, identidad visual, emblemas, iconos, plantillas ni contenido editorial. Los recursos de terceros conservan sus licencias originales (ver [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)).

El nombre "SOPRO" es utilizado bajo responsabilidad del titular del repositorio. Esta licencia no concede derechos exclusivos de marca ni exime al usuario de verificar la disponibilidad y compatibilidad de marcas registradas en su jurisdiccion.
