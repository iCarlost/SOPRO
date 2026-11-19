# ADR 0002: observabilidad y política global de fallos

Estado: **aprobada por el mantenedor el 2026-11-17; implementación de las Fases 0–4 completada**. La Fase 4 se completó en el PR #149 (issue #148). Este ADR se relaciona con el issue #132.

## Contexto

SOPRO ya contiene contención específica para fallos async de UI en materiales (#127) y para el recálculo global (#130), pero no tiene un mecanismo de diagnóstico persistente ni una política global para excepciones no observadas.

Actualmente:

- `Program.cs` no registra `Application.ThreadException`, `AppDomain.UnhandledException` ni `TaskScheduler.UnobservedTaskException`.
- `Debug.WriteLine` no deja evidencia en el ejecutable Release.
- Los errores de varias rutas solo muestran un `MessageBox`; algunos fallos de arranque/updater se descartan silenciosamente.
- Un fallo en un hilo no-UI puede terminar el proceso sin una traza disponible para soporte.
- Continuar después de un fallo fatal puede permitir escrituras con el estado de UI o de EF desconocido.

El cambio es estructural, pero no requiere crear un proyecto o una capa nueva. Los contratos y el sink vivirán en las capas existentes y el cableado de hooks quedará en WinForms.

## Decisión

### Clasificación de fallos

1. **Recuperable:** se contiene en la ruta, se notifica y se registra como `Warning`; la aplicación continúa.
2. **Resultado incierto:** se registra como `Error`, se suspende la escritura y se fuerza la reconciliación o relectura definida por la ruta. Nunca se afirma que hubo reversión.
3. **Fatal:** excepción no controlada en UI, excepción no terminante del dominio de aplicación o fallo de arranque que deja el estado desconocido. Se registra con flush síncrono, se muestra un diálogo propio y la aplicación se cierra con código distinto de cero. No se ofrece «Continuar».

`TaskScheduler.UnobservedTaskException` se registra y se marca observado; no se clasifica automáticamente como fatal, salvo que la operación que lo originó ya esté marcada como resultado incierto.

### Contrato y ubicación

- Crear un contrato inyectable `IDiagnosticsSink` en `SOPRO.Application/Contracts`, con `Write(DiagnosticsEvent)` y `Flush()`.
- Implementar el sink de archivo y el sink en memoria en `SOPRO.Application/Services`, sin dependencias NuGet nuevas.
- El evento es inmutable, con campos fijos (sin campo de metadatos libres), e incluye timestamp UTC, severidad, origen, operación, versión de producto, versión de esquema cuando aplique e identificador de correlación.
- Si hay excepción, el payload es conservador: nombre completo del tipo, `HResult` y cadena de tipos de las excepciones internas (máximo 8, aplanando `AggregateException`). No se registran `Exception.Message`, `Exception.StackTrace` ni `Exception.ToString()`, porque ese texto puede arrastrar rutas, nombres de proyecto y datos de usuario.
- La privacidad no depende solo de expresiones regulares: el contrato solo admite campos fijos y no tiene campo de metadatos libres, además de excluir el texto libre de excepciones. Sobre esos campos se sustituye el perfil por `%USERPROFILE%`, se eliminan caracteres de control, se redactan patrones obvios de secretos y se trunca la longitud.
- El logger será thread-safe, append/flush y tolerante a fallos: un error del propio logger nunca se propaga ni bloquea la UI. Cada línea anexada con éxito se vacía a disco de forma sincrónica; las líneas retenidas por timeout de candado siguen la política FIFO del punto siguiente.
- El sink de archivo toma un candado interproceso exclusivo (`FileShare.None` sobre `sopro-diagnostics.lock`) con espera acotada alrededor de la selección del archivo, el append, la rotación y la purga. Bajo contención transitoria la línea ya serializada queda en una cola FIFO **por instancia**, acotada a 64 líneas/256 KiB, que se vacía antes de la escritura en curso y por completo en `Flush`/`Dispose`. El desbordamiento de esa cola y los fallos permanentes notifican mediante el callback opcional de fallo ya existente; nunca se escribe fuera del candado y no hay garantía de orden global entre procesos.

### Almacenamiento y retención

- Directorio: `%LOCALAPPDATA%\SOPRO\logs\`, inyectable en pruebas.
- Archivo diario `sopro-YYYYMMDD.log` según la fecha UTC, con un límite aproximado de 1 MB.
- Rotación segmentada: al superar el límite, el archivo del día se renombra a `sopro-YYYYMMDD.1.log` y los segmentos existentes se desplazan (`sopro-YYYYMMDD.2.log`, etc.). El sufijo crece con la antigüedad: el archivo base es siempre el más reciente. El máximo de 10 archivos aplica al conjunto completo de segmentos, no por día.
- Retención inicial: máximo 7 días o 10 archivos en total, lo que ocurra primero. La purga reserva un hueco para el archivo del evento en curso, de modo que el conjunto nunca supere el máximo.
- Rotación y purga best-effort al arrancar y tras cada rotación, siempre dentro del candado interproceso.
- El candado es un archivo `sopro-diagnostics.lock` en el mismo directorio; no se borra al cerrar para evitar carreras entre instancias.
- Los logs no se guardarán junto al `.soproproj` ni en el directorio de instalación.
- Los registros no se borrarán automáticamente durante la desinstalación; el usuario tendrá una acción explícita para borrarlos.

### Privacidad

No se registrarán:

- contenido de celdas, tarifas, descripciones ni claves de insumos;
- tokens, cabeceras HTTP ni secretos;
- rutas con el segmento de perfil sin sanitizar.

Las rutas de proyecto tokenizarán el perfil del usuario como `%USERPROFILE%`, respetando límites de nombre de ruta (un perfil no debe sustituir el prefijo de otro nombre). La sanitización textual es defensa en profundidad, no la garantía principal: el evento no transporta mensajes ni trazas de excepción y su contrato no admite metadatos libres. El sistema será local y no subirá registros automáticamente. La exportación será una acción manual del usuario, con vista previa del contenido.

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

Definir el contrato, la sanitización, la matriz de eventos y los puntos de integración priorizados. Fase completada con la aprobación de este ADR el 2026-11-17.

### Fase 1 — Sink local (completada: PR #134)

Se implementaron el sink de archivo y el sink en memoria, con pruebas de formato, privacidad, concurrencia, rotación y fallo del propio sink. Esta fase no instaló hooks ni modificó `Program.cs`; el cableado de UI y el diálogo fatal se completaron en la Fase 2.

### Fase 2 — Hooks globales (completada: PR #135)

Se conectaron los hooks, el flush síncrono y el diálogo fatal. Se probaron excepciones UI, no-UI y tareas no observadas sin introducir la opción «Continuar».

### Fase 3 — Rutas críticas (completada: PRs #137, #139, #141, #143, #146 y #147)

Se adoptó el sink en las rutas priorizadas:

1. apertura de proyecto y migraciones, incluyendo versión de esquema y `M0XX` cuando estén disponibles;
2. arranque y updater, sustituyendo catches silenciosos por registro no intrusivo;
3. resultados inciertos ya existentes;
4. `Debug.WriteLine` de rutas críticas;
5. mensajes internos y correlación, sin exponer stack traces al usuario.

### Fase 4 — Privacidad y soporte (completada: PR #149)

Se añadió el aviso de primera ejecución, la exportación manual con vista previa, el borrado explícito y la documentación en `README.md`/`SUPPORT.md`.

El barrido de `async void` y fire-and-forget residual se completó con el issue [#151](https://github.com/iCarlost/SOPRO/issues/151) y el PR [#152](https://github.com/iCarlost/SOPRO/pull/152); la estabilización de pruebas y la retención bajo contención se endurecieron después en #155/#156 y #157/#158.

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

Se obtiene un diagnóstico local reproducible para fallos en campo, con una política conservadora ante estados desconocidos. El usuario deberá cerrar y reabrir la aplicación tras un fatal, y los registros locales en claro seguirán siendo información sensible indirecta; esto se mitiga con sanitización, retención limitada, ausencia de red y exportación manual. La aplicación no cablea hoy el callback opcional de fallo del sink, de modo que el descarte por desbordamiento o fallo permanente del archivo no se propaga a la UI.
