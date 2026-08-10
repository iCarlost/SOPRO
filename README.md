# 🏗️ SOPRO — Sistema de Presupuestación de Obra Pública

![CI](https://github.com/iCarlost/SOPRO/actions/workflows/ci.yml/badge.svg)
![Licencia](https://img.shields.io/badge/Licencia-MIT-blue.svg)

## 📌 Descripción

SOPRO es una aplicación de escritorio desarrollada en C# (.NET) orientada a la presupuestación de obra pública mediante análisis de precios unitarios (APU).

> **Estado de distribución:** el repositorio de código fuente es privado. Los instaladores publicados en `SOPRO-Releases` son un canal separado y no implican que el código fuente o futuros paquetes internos sean públicos.

El sistema integra en un solo entorno:

* Presupuesto de obra
* Matrices de precios unitarios
* Explosión de insumos
* Programa de obra (Gantt y Curva S)
* Análisis financiero

---

## 🧠 Arquitectura

La implementación actual sigue una arquitectura por capas:

```
SOPRO.Core        → Entidades
SOPRO.Application → Lógica de negocio
SOPRO.Data        → Persistencia (SQLite)
SOPRO.WinForms    → Interfaz de usuario
```

### ⚠️ Reglas de arquitectura

* No duplicar lógica ni mover lógica de negocio a la UI
* Los proyectos o dependencias estructurales nuevos requieren una decisión arquitectónica documentada
* El motor de cálculo debe ser único y consumirse a través de la ruta canónica
* La UI no debe acceder directamente a persistencia en los módulos desacoplados
* Los cambios de estructura no deben alterar resultados contables sin una decisión explícita

### Arquitectura objetivo y planes internos

La evolución planificada hacia un motor reutilizable y una UI WPF está documentada internamente en:

* [ADR-001: Arquitectura objetivo](ADR-001-ARQUITECTURA-OBJETIVO.md)
* [Plan 01: Desacoplamiento del núcleo y la aplicación](PLAN-01-DESACOPLAMIENTO-NUCLEO-APLICACION.md)
* [Plan 02: Migración modular de WinForms a WPF](PLAN-02-MIGRACION-WINFORMS-WPF.md)

---

## ⚙️ Tecnologías

* C# / .NET 8
* WinForms
* SQLite
* EF Core
* ClosedXML (Excel)
* PDFsharp / MigraDoc (PDF)

---

## 📊 Módulos principales

* Presupuesto
* Matrices (APU)
* Explosión de insumos
* Programa de obra
* Financiamiento
* Reportes

---

## 🧮 Motor de cálculo

El sistema utiliza un motor de cálculo centralizado que:

* Aplica precisión de pantalla
* Maneja redondeos controlados
* Calcula importes y precios unitarios
* Soporta insumos tipo `%MO`

---

## 🛠️ Compilar desde el código

Requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows).

```powershell
dotnet restore SOPRO.sln
dotnet build SOPRO.sln -c Release
```

## 🧪 Pruebas

El proyecto incluye pruebas unitarias en `SOPRO.Tests`:

```powershell
dotnet test SOPRO.sln
```

Las pruebas cubren cálculo de importes, motor de cálculo, redondeo, `%MO` e invariantes del sistema.

---

## 📦 Publicar una release

1. Actualizar versión en `SOPRO.WinForms.csproj` e `installer/SOPRO-InnoSetup.iss`.
2. Ejecutar el script de empaquetado (requiere [Inno Setup 6](https://jrsoftware.org/isdl.php)):

```powershell
.\build-release.ps1 -Version 1.6.0
```

3. Crear el release en GitHub y adjuntar `SOPRO-Setup-<versión>.exe`.

La app consulta las actualizaciones desde el repositorio de instaladores (`iCarlost/SOPRO-Releases`), por lo que cada versión distribuida debe publicarse allí con su etiqueta `vX.Y.Z`. Este canal de binarios es independiente de la visibilidad privada del repositorio fuente.

---

## 📁 Persistencia

* Base de datos: SQLite (.db)
* Un archivo por proyecto
* Manejo de esquema mediante SchemaManager

---

## 📦 Versionado

Ejemplo:

```
v1.4.3
```

* Cambios funcionales → minor
* Correcciones → patch
* Cambios estructurales → major

---

## ⚠️ Consideraciones importantes

* No romper compatibilidad de cálculo
* Mantener consistencia entre módulos
* Respetar precisión de pantalla
* Validar cambios con pruebas unitarias

---

## 🚀 Flujo de desarrollo

1. Realizar cambios
2. Ejecutar pruebas
3. Validar comportamiento
4. Actualizar versión
5. Generar instalador
6. Publicar en releases

---

## 🤝 Contribuciones

Consulta [CONTRIBUTING.md](CONTRIBUTING.md) antes de enviar un Pull Request.

---

## ⚖️ Licencia

SOPRO se distribuye bajo la licencia **MIT**. Ver [LICENSE](LICENSE).

---

## 👤 Autor

Carlos Pérez
