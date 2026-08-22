# Consumidor externo de validacion (`SOPRO.Calculation`)

Proyecto de consola **fuera de la solucion** `SOPRO.sln`. Su unico proposito es
validar el Gate N6: el paquete `SOPRO.Calculation` se instala desde un feed local,
compila y ejecuta correctamente sin referencia al proyecto.

- El `nuget.config` de esta carpeta usa `<clear />` y solo apunta a
  `.artifacts/packages` (feed local). Si el paquete tuviera dependencias runtime,
  el restore fallaria aqui.
- `Program.cs` ejecuta aserciones exactas sobre redondeo, cascada de porcentajes,
  distribucion con residuo y suma de costo directo; sale con codigo distinto de
  cero si cualquier valor difiere.

## Uso

Desde la raiz del repositorio:

```powershell
dotnet pack SOPRO.Calculation/SOPRO.Calculation.csproj -c Release --output .artifacts/packages --nologo
dotnet run --project consumers/Sopro.Calculation.Consumer -c Release
```

Salida esperada:

```text
External consumer validation passed.
```
