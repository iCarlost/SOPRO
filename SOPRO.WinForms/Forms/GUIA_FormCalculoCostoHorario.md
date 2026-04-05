# FormCalculoCostoHorario - Guía de Implementación del Designer

## 🎯 ESTRUCTURA DEL FORM

El FormCalculoCostoHorario usa **TabControl** con 3 pestañas:

```
┌─────────────────────────────────────────────────┐
│ 🧮 CÁLCULO DE COSTO HORARIO                    │
├─────────────────────────────────────────────────┤
│ Información General:                            │
│ Clave: MQ-CV | Descripción: Camión Volteo...   │
│ Potencia: 110 HP | Combustible: Diesel         │
├─────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────┐ │
│ │ [A. CARGOS FIJOS] [B. CONSUMOS] [C. OPERAC]│ │
│ └─────────────────────────────────────────────┘ │
│                                                 │
│ (CONTENIDO DEL TAB ACTIVO)                     │
│                                                 │
├─────────────────────────────────────────────────┤
│ COSTO HORARIO TOTAL: $285.50 /hora             │
│ [Restaurar Defecto] [Cancelar] [💾 Guardar]   │
└─────────────────────────────────────────────────┘
```

---

## 📋 CONTROLES NECESARIOS

### **Panel Superior**
- `panelTop` - Color #33334C
- `lblTitulo` - "🧮 Cálculo de Costo Horario"

### **Panel Información**
- `panelInfo` - Fondo blanco
- Labels informativos (solo lectura):
  - `lblClave`, `lblDescripcion`, `lblPotencia`, `lblCombustible`

### **TabControl**
- `tabControl` con 3 tabs:
  - `tabCargosFijos` - "A. CARGOS FIJOS"
  - `tabConsumos` - "B. CONSUMOS"
  - `tabOperacion` - "C. OPERACIÓN"

---

## 🅰️ TAB 1: CARGOS FIJOS

### GroupBox: Depreciación
```
nudValorAdquisicion      (0 - 9,999,999.99)
nudValorLlantas         (0 - 999,999.99)
nudValorPiezasEsp       (0 - 999,999.99)
nudFactorRescate        (0 - 1.00, decimales: 2, default: 0.10)
nudVidaEconomica        (1 - 50,000, decimales: 2)
```

### GroupBox: Inversión
```
nudTasaInteres          (0 - 100, decimales: 4, default: 21.24)
nudHorasAnio            (1 - 5,000, decimales: 2, default: 1600)
```

### GroupBox: Seguros
```
nudPrimaSeguro          (0 - 100, decimales: 4, default: 3.00)
```

### GroupBox: Mantenimiento
```
nudFactorManten         (0 - 5, decimales: 6, default: 0.20)
```

### GroupBox: Resultados (lado derecho)
```
lblDepreciacion         - Resultado D
lblInversion            - Resultado Im
lblSeguros              - Resultado Sm
lblMantenimiento        - Resultado Mn
lblTotalCargosFijos     - TOTAL (destacado, bold)
```

---

## 🅱️ TAB 2: CONSUMOS

### GroupBox: Combustible
```
nudCantCombustible      (0 - 100, decimales: 4) lts/hr
nudPrecioCombustible    (0 - 999.99, decimales: 4) $/lt
```

### GroupBox: Lubricantes
```
nudCantAceite           (0 - 10, decimales: 4) lts/hr
nudPrecioAceite         (0 - 999.99, decimales: 4) $/lt
```

### GroupBox: Llantas
```
nudNumLlantas           (0 - 20, entero)
nudVidaLlantas          (1 - 50,000, decimales: 2) horas
```

### GroupBox: Piezas Especiales
```
nudVidaPiezasEsp        (1 - 50,000, decimales: 2) horas
```

### GroupBox: Resultados
```
lblCombustible          - Resultado Co
lblLubricantes          - Resultado Lb
lblLlantas              - Resultado Nt
lblPiezasEsp            - Resultado Ae
lblTotalConsumos        - TOTAL (bold)
```

---

## ©️ TAB 3: OPERACIÓN

### GroupBox: Salario del Operador
```
nudSalarioOperador      (0 - 99,999.99, decimales: 2)
nudFSR                  (1 - 5, decimales: 6, default: 1.6543)
nudHorasTurno           (1 - 24, decimales: 2, default: 8)
```

### GroupBox: Resultados
```
lblOperacion            - Resultado Po (bold)
```

---

## 🎨 PANEL INFERIOR

### Controles:
```
lblCostoTotal           - Tamaño grande (18pt), bold, verde
label39                 - "COSTO HORARIO TOTAL:"
btnRestaurarDefecto     - Gris
btnCancelar             - Gris
btnGuardar              - Verde (#4CAF50)
```

---

## 🎯 DISTRIBUCIÓN VISUAL

### Layout Sugerido:

**Cada Tab tiene:**
- Izquierda (60%): GroupBoxes con campos de entrada
- Derecha (40%): GroupBox "Resultados" con labels de solo lectura

**Tamaños:**
- Form: 1000 x 700
- TabControl ocupa todo el espacio disponible
- Panel Bottom: 80px de altura

---

## 💡 TIPS DE DISEÑO

1. **Colores de Resultados:**
   - Depreciación, Inversión, etc.: Color normal
   - TOTALES: Bold, color verde oscuro (#2E7D32)

2. **NumericUpDown:**
   - TextAlign: Right
   - ThousandsSeparator: true

3. **GroupBoxes:**
   - Font: Segoe UI, 9.5pt, Bold
   - Padding: 10px

4. **Labels de Resultado:**
   - Font: Segoe UI, 11pt
   - Formato: `${value:N2}`

---

## 🧮 FÓRMULAS IMPLEMENTADAS (Referencia)

### Cargos Fijos:
- **D** = (Valor Neto - Valor Rescate) / Vida Económica
- **Im** = [(Vm + Vr) / 2] × (i / 100) / Hea
- **Sm** = [(Vm + Vr) / 2] × (s / 100) / Hea
- **Mn** = Ko × D

### Consumos:
- **Co** = Cantidad × Precio
- **Lb** = Cantidad × Precio
- **Nt** = (Núm. Llantas × Valor) / Vida
- **Ae** = Valor / Vida

### Operación:
- **Po** = (Salario × FSR) / Horas Turno

### Total:
- **COSTO HORARIO** = CF + Consumos + Operación

---

## ✅ CHECKLIST DE IMPLEMENTACIÓN

- [ ] Crear panelTop con título
- [ ] Crear panelInfo con 4 labels informativos
- [ ] Crear TabControl con 3 tabs
- [ ] Tab A: 4 GroupBoxes + 1 Resultados
- [ ] Tab B: 4 GroupBoxes + 1 Resultados  
- [ ] Tab C: 1 GroupBox + 1 Resultados
- [ ] Panel Bottom con 3 botones y label total
- [ ] Configurar todos los NumericUpDown
- [ ] Configurar todos los Labels de resultado
- [ ] Probar cálculo en tiempo real

---

## 📝 ORDEN DE CREACIÓN RECOMENDADO

1. Estructura básica (panels, tabControl)
2. Tab Cargos Fijos (más complejo primero)
3. Tab Consumos
4. Tab Operación (más simple)
5. Panel Bottom
6. Ajustar tamaños y espaciado

---

*Este form es la JOYA del sistema - tómate tu tiempo para hacerlo bien visual!*
