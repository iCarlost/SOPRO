# ADR 0002 (propuesta): observabilidad y política global de fallos

Estado: **propuesta**. Este documento requiere aprobación del mantenedor antes de implementar código. Se relaciona con el issue #132.

## Contexto

SOPRO ya contiene contención específica para fallos async de UI en materiales (#127) y para el recálculo global (#130), pero no tiene un mecanismo de diagnóstico persistente ni una política global para excepciones no observadas.

Actualmente:

- `Program.cs` no registra `Application.ThreadException`, `AppDomain.UnhandledException` ni `TaskScheduler.UnobservedTaskException`.
- `Debug.WriteLine` no deja evidencia en el ejecutable Release.
- Los errores de varias rutas solo muestran un `MessageBox`; algunos fallos de arranque/updater se descartan silenciosamente.
- Un fallo en un hilo no-UI puede terminar el proceso sin una traza disponible para soporte.
- Continuar después de un fallo fatal puede permitir escrituras con el estado de UI o de EF desconocido.

El cambio es estructural, pero no requiere crear un proyecto o una capa nueva. Los contratos y el sink vivirán en las capas existentes y el cableado de hooks quedará en WinForms.

## Decisión propuesta

### Clasificación de fallos

1. **Recuperable:** se contiene en la ruta, se notifica y se registra como `Warning`; la aplicación continúa.
2. **Resultado incierto:** se registra como `Error`, se suspende la escritura y se fuerza la reconciliación o relectura definida por la ruta. Nunca se afirma que hubo reversión.
3. **Fatal:** excepción no controlada en UI, excepción no terminante del dominio de aplicación o fallo de arranque que deja el estado desconocido. Se registra con flush síncrono, se muestra un diálogo propio y la aplicación se cierra con código distinto de cero. No se ofrece «Continuar».

`TaskScheduler.UnobservedTaskException` se registra y se marca observado; no se clasifica automáticamente como fatal, salvo que la operación que lo originó ya esté marcada como resultado incierto.

### Contrato y ubicación

- Crear un contrato inyectable, por ejemplo `IDiagnosticsSink`, en `SOPRO.Application/Contracts`.
- Implementar el sink de archivo en `SOPRO.Application/Services`, sin dependencias NuGet nuevas.
- Proporcionar un sink en memoria para pruebas.
- El evento incluirá timestamp UTC, severidad, origen, operación, excepción completa, versión de producto, versión de esquema cuando aplique e identificador de correlación.
- El logger será thread-safe, append/flush y tolerante a fallos: un error del propio logger nunca se propaga ni bloquea la UI.

### Almacenamiento y retención

- Directorio: `%LOCALAPPDATA%\SOPRO\logs\`.
- Archivos diarios `sopro-YYYYMMDD.log`, con un límite aproximado de 1 MB.
- Retención inicial: máximo 7 días o 10 archivos, lo que ocurra primero.
- Rotación y purga best-effort al arrancar.
- Los logs no se guardarán junto al `.soproproj` ni en el directorio de instalación.
- Los registros no se borrarán automáticamente durante la desinstalación; el usuario tendrá una acción explícita para borrarlos.

### Privacidad

No se registrarán:

- contenido de celdas, tarifas, descripciones ni claves de insumos;
- tokens, cabeceras HTTP ni secretos;
- rutas con el segmento de perfil sin sanitizar.

Las rutas de proyecto tokenizarán `%USERPROFILE%` como `%USERPROFILE%`. El sistema será local y no subirá registros automáticamente. La exportación será una acción manual del usuario, con vista previa del contenido.

### Hooks y experiencia fatal

Los hooks se instalarán antes de crear ventanas:

- `Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)`;
- `Application.ThreadException`;
- `AppDomain.CurrentDomain.UnhandledException`, respetando `IsTerminating`;
- `TaskScheduler.UnobservedTaskException`.

El diálogo fatal propio tendrá mensaje en español, identificador de correlación, ruta de registros, acción «Abrir carpeta de registros» y acción «Cerrar aplicación». No ofrecerá continuación. En eventos fuera del hilo de UI no se mostrará `MessageBox` directamente: se registrará y se cerrará de forma segura.

Todos los sinks y diálogos deberán poder sustituirse en pruebas para no romper la suite STA ni abrir modales no interceptables.

## Plan de ejecución

### Fase 0 — Contrato y ADR

Definir el contrato, la sanitización, la matriz de eventos y los puntos de integración priorizados. Esta fase termina con la aprobación explícita de este ADR.

### Fase 1 — Sink local

Implementar el sink de archivo y el sink en memoria, con pruebas de formato, privacidad, concurrencia, rotación y fallo del propio sink.

### Fase 2 — Hooks globales

Conectar los hooks, el flush síncrono y el diálogo fatal. Probar excepciones UI, no-UI y tareas no observadas sin introducir la opción «Continuar».

### Fase 3 — Rutas críticas

Adoptar el sink, en este orden:

1. apertura de proyecto y migraciones, incluyendo versión de esquema y `M0XX` cuando estén disponibles;
2. arranque y updater, sustituyendo catches silenciosos por registro no intrusivo;
3. resultados inciertos ya existentes;
4. `Debug.WriteLine` de rutas críticas;
5. mensajes internos y correlación, sin exponer stack traces al usuario.

### Fase 4 — Privacidad y soporte

Añadir aviso de primera ejecución, exportación manual con vista previa, borrado explícito y documentación en `README.md`/`SUPPORT.md`.

El barrido de `async void` y fire-and-forget residual queda fuera de este ADR y se tratará en otro issue.

## Alternativas descartadas

- **Permitir «Continuar» tras un fallo fatal:** puede continuar con UI, contexto EF o estado de persistencia desconocido.
- **Registrar solo con `Debug.WriteLine`:** no produce evidencia en Release.
- **Guardar logs junto al proyecto:** contamina carpetas de usuario y mezcla diagnóstico con datos del proyecto.
- **Crear un proyecto `SOPRO.Diagnostics`:** añade una capa nueva sin necesidad; el contrato puede vivir en `Application` y el cableado en `WinForms`.
- **Telemetría o subida automática:** aumenta superficie de privacidad y seguridad; queda fuera de alcance.
- **Sustituir todos los `MessageBox` o todos los `async void` en esta iniciativa:** ampliaría el cambio sin mejorar primero la observabilidad central.

## Puertas de verificación

- El ADR está aprobado antes de editar producción.
- Un fallo del sink no termina el proceso ni bloquea la UI.
- Un fatal se registra, se vacía a disco y cierra sin «Continuar».
- `UnobservedTaskException` se registra y se marca observado según la política.
- Pruebas automatizadas demuestran sanitización y ausencia de datos prohibidos.
- Rotación, retención, exportación y borrado son verificables.
- La suite STA no queda bloqueada por el diálogo fatal.
- Build de solución y suite completa pasan; los dos hashes golden PDF conocidos permanecen fuera de alcance.

## Consecuencias

Se obtendrá un diagnóstico local reproducible para fallos en campo, con una política conservadora ante estados desconocidos. El usuario deberá cerrar y reabrir la aplicación tras un fatal, y los registros locales en claro seguirán siendo información sensible indirecta; esto se mitiga con sanitización, retención limitada, ausencia de red y exportación manual.
