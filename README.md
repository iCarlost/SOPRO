# 🏗️ SOPRO — Sistema de Presupuestación de Obra Pública

![CI](https://github.com/iCarlost/SOPRO/actions/workflows/ci.yml/badge.svg)
![Licencia](https://img.shields.io/badge/Licencia-MIT-blue.svg)

## 📌 Descripción

SOPRO es una aplicación de escritorio desarrollada en C# (.NET) orientada a la presupuestación de obra pública mediante análisis de precios unitarios (APU).

El sistema integra en un solo entorno:

* Presupuesto de obra
* Matrices de precios unitarios
* Explosión de insumos
* Programa de obra (Gantt y Curva S)
* Análisis financiero

---

## 🧠 Arquitectura

El sistema sigue una arquitectura por capas:

```
SOPRO.Core        → Entidades
SOPRO.Application → Lógica de negocio
SOPRO.Data        → Persistencia (SQLite)
SOPRO.WinForms    → Interfaz de usuario
```

### ⚠️ Reglas de arquitectura

* No crear nuevas capas
* No duplicar lógica
* Reutilizar servicios existentes
* No mover lógica a UI
* El motor de cálculo debe ser único

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

La app consulta las actualizaciones desde el repositorio de releases (`iCarlost/SOPRO-Releases`), por lo que cada versión debe publicarse allí con su etiqueta `vX.Y.Z`.

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
