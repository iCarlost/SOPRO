# 🏗️ SOPRO — Sistema de Presupuestación de Obra Pública

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

* C# / .NET
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

## 🧪 Pruebas

El proyecto incluye pruebas unitarias en:

```
SOPRO.Tests
```

Las pruebas cubren:

* Cálculo de importes
* Motor de cálculo
* Redondeo
* %MO
* Invariantes del sistema

### Ejecución

En Visual Studio:

```
Test → Run All Tests
```

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

## 🔐 Componentes sensibles

NO incluidos en este repositorio:

* Licenciador
* Clave privada de firma

---

## 🚀 Flujo de desarrollo

1. Realizar cambios
2. Ejecutar pruebas
3. Validar comportamiento
4. Actualizar versión
5. Generar instalador
6. Publicar en releases

---

## 👤 Autor

Carlos Pérez
