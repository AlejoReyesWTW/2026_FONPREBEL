# Fon Prebel — Aplicativo de escritorio de pólizas de jubilados

Objetivo: aplicación WPF de escritorio que automatice el procesamiento mensual de pólizas en 5 fases, operada por un analista que confirma entre fases.
Spec fuente: `Specs/especificacion-fon-prebel-v0.10.md` (1935 líneas, versión 0.10).
Rama: `feature/fonprebel-app` (se crea en el primer commit de código). Entrega: `ask-on-risk`. Runner: `dotnet test` (xUnit).

---

## Specs

S1. C# / .NET. UI WPF con el paquete oficial **WPF-UI** de Lepoco (NuGet `WPF-UI`, repo `lepoco/wpfui`), "No se usan forks ni paquetes de terceros con nombre parecido". MVVM. Capas Presentation / Application / Domain / Infrastructure con las responsabilidades de §2.2 y la estructura orientativa de un único `FonPrebel.csproj`. Logo en `Assets/`.
Decisión del usuario (D-01, D-03): **.NET 8 LTS** (`net8.0-windows`) para que el proyecto abra y corra tanto en Visual Studio 2022 como en Visual Studio 2026.
Aceptación: la solución compila con esa estructura y la UI usa controles WPF-UI, sin lógica de negocio en Presentation.

S2. "**Ningún archivo de código fuente podrá superar las 400 líneas de código.**" Exentos: migraciones EF, `ModelSnapshot`, `.Designer.cs`, documentación, scripts SQL, configuración y recursos.
Aceptación: script de verificación que falle si un archivo de código supera 400 líneas.

S3. Persistencia local SQLite con **EF Core** (proveedor SQLite) + Fluent API en `Infrastructure/Database`. "No se escribe SQL en texto dentro del código". Contextos de vida corta vía `IDbContextFactory`, uno por operación, nunca compartido entre hilos. WAL y tiempo de espera ante bloqueo activados. Migraciones versionadas en el repositorio, aplicadas de forma controlada al iniciar, sin borrar historial ni recrear la base. Documentar en `database/DatabaseDictionary.md`.
Aceptación: la base se crea desde cero con migraciones; reiniciar no pierde datos; ninguna cadena SQL en el código.

S4. Esquema candidato: `Batch`, `BatchExecution`, `BatchRecipient`, `PhaseExecution`, `EmailConfiguration`, `AppConfiguration`. Relaciones, columnas, claves, restricciones e índices deben diseñarse y documentarse **antes** de implementar. "no se deben inventar campos de negocio que aún no estén definidos".
Aceptación: `DatabaseDictionary.md` aprobado antes de la primera migración.

S5. Datos locales en `%LOCALAPPDATA%/FonPrebel/` con `Database/FonPrebel.db`, `Logs/YYYY-MM-DD/{execution.log,errors.log}` y `Salida/`. La base no se guarda en la carpeta de instalación ni en el servidor de archivos.
Aceptación: con el usuario sin permisos de administrador, la app escribe en esas rutas.

S6. Configuración administrada desde la pantalla Configuración y guardada en SQLite. "No se utiliza un archivo `.env`". "Ningún parámetro tiene un valor por defecto inventado". Único valor confirmado: intervalo de verificación automática = **2 minutos**.
Aceptación: ningún parámetro persistido con valor por defecto inventado; la pantalla permite editar y validar.

S7. Credenciales cifradas con **DPAPI** (`CurrentUser`, a validar con TI), descifradas bajo demanda, nunca en logs ni visibles en pantalla. Campo de contraseña enmascarado con botón de ojo y modo edición con lápiz (anterior + nueva + confirmación), validación remota antes de reemplazar de forma atómica. Alerta previa al inicio: `"Por favor, configure las credenciales necesarias para el envío de correos"`.
Aceptación: la contraseña nunca se escribe en claro en SQLite, logs ni pantalla; el reemplazo es atómico.

S8. Lotes, ejecuciones y destinatarios con reanudación (§2.8, 12 reglas): cada lote con identificador único y legible; reanudar no crea lote nuevo; los correos confirmados no se reenvían; el historial no se sobrescribe ni se borra; el estado incierto de envío se representa de forma explícita; "La aplicación no debe afirmar que un correo fue enviado si no recibió la confirmación correspondiente".
Aceptación: cerrar la app a mitad de lote y reabrir ofrece continuar sin duplicar ni reenviar.

S9. **Fase 1 — Consolidación**: abrir el consolidado de la ruta 1, crear la hoja del mes actual en mayúsculas (`NOVIEMBRE`), con las 8 columnas del Anexo A, copiando desde los 16 archivos de póliza de la ruta 2. Al terminar, "detenerse y pedir confirmación del analista" antes de la fase 2. El mapeo de columnas de origen está pendiente (§5.1).
Aceptación: la hoja del mes existe con las 8 columnas y el estado queda persistido por lote y ejecución.

S10. **Fase 2 — Cruce y completado**: tomar el archivo del analista de la ruta 3, completar los valores en blanco de `CUOTA MENSUAL` de las pólizas usando la hoja consolidada, guardar sobre la **misma ruta** del archivo, y esperar la confirmación del analista. La llave del cruce está pendiente (§5.1).
Aceptación: los valores completados son correctos y el archivo se guarda en su ruta original sin corromperse.

S11. **Anexo C — `TOTAL MENSUAL POLIZA`**: agrupar por `CEDULA ASOCIADO` + `#POLIZA`; sumar `CUOTA MENSUAL` una sola vez por grupo; escribir el total en una única celda combinada verticalmente que cubre exactamente las filas del grupo; filas del grupo consecutivas; grupo de un registro sin combinación; no repetir el total por fila; no sumar entre asociados distintos. Ejemplo del spec: `$795.060` + `$795.060` + `$1.114.470` = `$2.704.590`.
Aceptación: prueba con el ejemplo del Anexo C que verifique valor y combinación de celdas.

S12. **Fase 3 — PDFs**: un PDF por cédula en la ruta 4, con los rubros del jubilado "mostrados en dos tablas", cifrado con la cédula **inmediatamente después** de generarlo. Progreso = PDFs generados / total. Cifrado y diseño detallado pendientes (§2.11, §3).
Aceptación: un PDF por cédula, abre con la cédula como contraseña, progreso correcto, resultado por destinatario persistido.

S13. **Fase 4 — Correo**: un PDF por correo individual, el destinatario recibe sólo su archivo; sin correo = no enviado, registrado y excluido de "correos disponibles"; envío secuencial en segundo plano, "nunca de forma masiva"; progreso enviados/disponibles (ejemplo del spec: `20 de 40 (50%)`); reanudar no reenvía confirmados; pendiente / fallido / incierto se muestran distintos. Política de reintentos pendiente (§5.2).
Aceptación: ningún correo confirmado se reenvía; los inciertos no se reportan como enviados.

S14. **Fase 5 — Informe**: Excel con dos tablas (notificados / no notificados con su motivo) guardado en `Salida/` local — no en el servidor compartido — identificando el lote. Columnas y nombre pendientes (§3).
Aceptación: el informe se genera cuando todas las fases 2-4 terminaron y refleja el estado real.

S15. Concurrencia y progresividad: la UI nunca se congela; trabajo en segundo plano con `CancellationToken`; contexto de base por worker; correos uno por uno; fases 2, 3 y 4 avanzando por jubilado de forma coexistente; fase 5 sólo cuando todo terminó; el nivel de paralelismo está pendiente (§2.15) y la fórmula del progreso general también (§2.16).
Aceptación: con la UI respondiendo, las tres fases muestran avance simultáneo y cancelar detiene de forma segura.

S16. UI: navegación lateral con Ejecución, Historial, Logs, Configuración, Métricas, Conexiones e Inicio de sesión; indicador de fases con un punto por fase y spinner; barras por fase y global; fondo blanco, logo grande como marca de agua, componentes ligeramente transparentes, tema oscuro opcional.
Aceptación: las siete secciones existen y el indicador de fases refleja el estado persistido.

S17. Logs: `execution.log` y `errors.log` por día en carpeta `YYYY-MM-DD`, visibles en dos paneles de la UI, con fecha/hora, nivel, lote, ejecución, fase y resultado. Nunca en SQLite. Sin secretos ni datos sensibles.
Aceptación: ambas bitácoras se escriben y se leen desde la UI; ningún secreto aparece.

S18. **Anexo D**: el botón INICIAR permanece deshabilitado hasta que toda la configuración obligatoria sea válida, con el aviso `"Completa todos los campos obligatorios de Configuración para habilitar el inicio del proceso."` en tono suave amarillo/naranja; se vuelve a deshabilitar si un valor se invalida.
Aceptación: prueba que verifica habilitado/deshabilitado y el aviso.

S19. **Anexo E**: validación por campo con spinner y estado verde/rojo que se revalida al cambiar; monitoreo de recursos y parada segura; alerta crítica modal con Qué ocurrió, Dónde, Qué estaba haciendo, Por qué se detuvo, Qué debe hacer y Estado del proceso; plantilla `PROCESO DETENIDO — ARCHIVO NO DISPONIBLE` con `[ruta del archivo]`, `[nombre del archivo]`, `[etapa del proceso]`, `[detalle del error detectado]`; cerrar la alerta no reanuda el proceso.
Aceptación: una desconexión del servidor de archivos produce esa alerta y el proceso queda detenido hasta confirmación del analista.

S20. **Anexo F — recuperación**: puntos de control, huellas (hash) de los archivos de entrada recalculadas al reanudar, estados `LISTO`, `EJECUTANDO`, `PAUSADO POR INCONSISTENCIA`, `VALIDANDO RECUPERACIÓN`, `LISTO PARA REANUDAR`, `COMPLETADO`, `CANCELADO`; reanudar es idempotente: no duplica PDFs, hojas ni registros, descarta resultados temporales incompletos y conserva los válidos.
Aceptación: interrumpir en cada fase y reanudar deja el estado consistente y sin duplicados.

S21. Instalación: "se evaluará una publicación autocontenida para Windows y un instalador autorizado por TI". La app debe funcionar en el equipo del analista y también en otra máquina (requisito del usuario: máquina personal para probar, máquina del trabajo al terminar). Nada de rutas absolutas ni supuestos de la máquina de desarrollo.
Aceptación: el paquete publicado corre en una segunda máquina sin instalar el SDK y sin tocar código.

S22. Inicio de sesión contra SQL Server central: **etapa final**, sólo cuando se indique. `IAuthenticationService` en Application, DbContext y proveedor independientes de la base local; la app no crea tablas en la base central; si la base central no está disponible, el acceso se bloquea (decisión pendiente §2.23).
Aceptación: no se implementa hasta recibir la orden y los datos de conexión.

S23. Prohibiciones: sin Mediator, buses, CQRS, repositorios genéricos ni patrones "por moda" (§2.4); sin `.env`; sin SQL en texto; sin logs en SQLite; sin inventar campos, rutas, llaves, valores por defecto ni logo; no es un bot desatendido — el analista opera y confirma (§1, §4).

---

## Tasks

| ID | Tarea | Specs | Ruta | Estado | Evidencia |
|----|-------|-------|------|--------|-----------|
| D1 | Diseño de arquitectura y decisiones técnicas (versión .NET, paquetes, licencias, estructura, DI, logging, rutas) | S1,S2,S3,S5,S17,S21 | inline | hecho | `docs/diseno/01-arquitectura.md` |
| D2 | Modelo de datos + `DatabaseDictionary.md` | S3,S4,S8,S20 | inline | hecho | `docs/diseno/02-modelo-datos.md` §1–§3 y §7 |
| D3 | Diseño de pantallas (7 secciones, gate INICIAR, alerta crítica, modal de seguimiento) | S16,S18,S19 | inline | hecho | `docs/diseno/03-pantallas.md` (219 líneas, 10 fichas V1–V10) |
| D4 | Máquina de estados, reanudación, puntos de control y huellas | S8,S20 | inline | hecho | `docs/diseno/02-modelo-datos.md` §4–§6 |
| D5 | Matriz de configuración y credenciales con criterios de validación | S6,S7 | inline | hecho | `docs/diseno/04-configuracion.md` |
| A1 | Solución en Visual Studio: proyecto WPF + WPF-UI + MVVM + DI + navegación de 7 secciones | S1,S16 | hecho | `FonPrebel.sln` + `src/FonPrebel` + 6 pruebas en verde |
| A2 | Rutas de datos locales, Serilog con `execution.log`/`errors.log` y paneles en UI | S5,S17 | pendiente | | |
| A3 | EF Core SQLite: contextos de vida corta, WAL, migraciones y auto-aplicación al iniciar | S3 | pendiente | | |
| A4 | Proveedor de credenciales DPAPI | S7 | pendiente | | |
| A5 | Pantalla Configuración con validación por campo y gate de INICIAR | S6,S18 | pendiente | | |
| A6 | Pantalla Conexiones: prueba de servidor de archivos y de correo, punto de estado, rechequeo cada 2 minutos | S19 | pendiente | | |
| A7 | Lotes, ejecuciones, historial y reanudación con huellas | S8,S20 | pendiente | | |
| A8 | Alertas críticas modales, notificaciones toast y modal de seguimiento | S16,S19 | pendiente | | |
| A9 | Métricas desde SQLite | S16 | pendiente | | |
| A10 | Lector tolerante de encabezados: normaliza tildes, espacios, mayúsculas y `#` para comparar `RIESGO ASEGURADO`/`RIESGO ASOCIADO`, `COMPAÑÍA`/`COMPANIA`, `# PÓLIZA`/`#POLIZA`, `CORREO ELECTRONICO`/`CORREO ELECTRÓNICO` | S9,S10,S11 | pendiente | | |
| B1 | Fase 1 consolidación: lectura de los 16 archivos y escritura de la hoja del mes | S9 | pendiente | | |
| B2 | Fase 2 cruce y completado del archivo del analista | S10 | pendiente | | |
| B3 | Anexo C: `TOTAL MENSUAL POLIZA` con celdas combinadas | S11 | pendiente | | |
| B4 | Fase 3: PDFs por cédula con cifrado | S12 | pendiente | | |
| B5 | Fase 4: envío secuencial de correo con estados | S13 | pendiente | | |
| B6 | Fase 5: informe Excel de notificaciones | S14 | pendiente | | |
| B7 | Pipeline progresivo por jubilado (2→3→4), concurrencia, cancelación y progreso | S15 | pendiente | | |
| C1 | Pruebas unitarias e integrales con archivos de muestra | S9-S14,S20 | pendiente | | |
| C2 | Publicación autocontenida + instalador + guía de instalación | S21 | pendiente | | |
| C3 | Verificación en una segunda máquina | S21 | pendiente | | |
| C4 | Inicio de sesión contra SQL Server central (etapa final, sólo si se ordena) | S22 | pendiente | | |

---

## Decisiones abiertas (todas vienen del spec, no del diseño)

Cerradas por el usuario el 2026-11-06: versión .NET 8 LTS con compatibilidad VS 2022 + VS 2026 (D-01, D-03); biblioteca PDFsharp + MigraDoc MIT (D-02); sin datos reales por ahora, el bloque A avanza sin archivos (D-04).

Bloquean fases de negocio: rutas reales (§5.1), mapeo de columnas de los 16 archivos (B1), llave del cruce (B2), contenido y diseño del PDF (B4), protocolo de correo y proxy (B5), política de reintentos e inciertos (B5), columnas y nombre del informe (B6), formato del identificador de lote y estados exactos (A7), fórmula del progreso general (B7).
Bloquean empaquetado: política de TI sobre instaladores y permisos (§5.5), ubicación de `Salida/` y ámbito DPAPI.
Fuera de alcance hasta orden explícita: inicio de sesión con SQL Server (§2.23, "etapa final").

---

## Log

L1 — 2026-11-06 — Pedido del usuario (verbatim): "porfa lee la espec que esta en la carpeta de specs, y ame el plan de tareas delega , sigue todo al pie de la letra, la idea es isar visual studio, la idea es que debe fncionar en mi maquina personal y en otras mas, el aplicativo de escritorio se probara aqui y cuando se termine debe funcionar en mi maquina del trabajo, es claro?"
L2 — 2026-11-06 — Pedido del usuario (verbatim): "genera un plan de trabajo dime si tienes dudas, , empieza por el diseño y genera el chek list de todos que haremos"
L3 — 2026-11-06 — Evidencia: mapa completo del spec (lectura de las 1935 líneas). Hallazgos relevantes: §6 regla 2 prohíbe inventar pendientes; §7 cierra con "La versión 0.9 no representa todavía una especificación cerrada ni autorización para comenzar todas las funcionalidades"; el `.docx` "Bot Cuentas de Cobros Jubilados" no está referenciado en ninguna parte del spec; no hay sección que defina en detalle el modal de seguimiento en vivo.
L4 — 2026-11-06 — Evidencia de archivos reales: el consolidado ya trae hojas MARZO..SEPTIEMBRE con 8 columnas y a veces una novena (`COBRO CIA`); los encabezados reales son `RIESGO ASEGURADO`, `COMPAÑÍA`, `# PÓLIZA` (con espacio) y en el archivo del analista `CORREO ELECTRONICO`, distintos de los del Anexo A/B (`RIESGO ASOCIADO`, `COMPAÑIA`, `#POLIZA`, `CORREO ELECTRÓNICO`). El archivo del analista agrupa por jubilado dejando `CEDULA ASOCIADO` y `NOMBRE ASOCIADO` en blanco en las filas de continuación, lo que confirma el comportamiento del Anexo C.
L5 — 2026-11-06 — Respuestas del usuario a las cuatro preguntas de diseño (verbatim):
- Versión: "pues yo uso visual studio 2026. la idea es que sirva con el 8 creo, que lo abra el visual studio 2022 y lo ejecute sim problemas" → D-01/D-03: .NET 8 LTS, compatible con VS 2022 y VS 2026.
- PDF: "la que tenga soporte, buena documentacion, que sea segura, que se use mucho por la comunidad , nada de librrias super raras" → D-02: PDFsharp + MigraDoc (MIT, contraseña de apertura).
- Archivos: "por ahora necesitamos empezar por el diseño, ese detalle como rutas de los archivos no los tengo y por ende no tengo acceso a los archivos, para darte esa informacion, por ahora necsito empezar con el diseño de todo, y si hay ago de backend que se pueda hacer pus agregalo al todo" → D-04: el diseño avanza completo y el bloque A del backend también; se agrega A10.
- `.docx`: "donde, yo no veo ese archivo, por ahora ignoralo , hasta ahora se que existe ese archivo" → fuera de alcance. El archivo existe en la raíz del repo como `COL-AFF-PRBL-Bot Cuentas de Cobros Jubilados-AUT4565083531 F.docx` y no se lee como requisito.
L6 — 2026-11-06 — Decisión de ruteo: el gate de writers exige identificadores `T<n>` y este documento usa `D/A/B/C`. En lugar de renombrar tareas para satisfacer el gate, el diseño se escribe inline. Si más adelante hace falta delegar unidades en paralelo, se agrega un alias `Tnn` en la tabla de tareas.
L7 — 2026-11-06 — Lectura directa de §2.9.1 (líneas 414–431) y corrección de D2: la tabla del spec ubica **proxy activado, texto del proxy y tiempo máximo de espera por envío en `AppConfiguration`**, no en `EmailConfiguration`; y no existe un parámetro "requiere autenticación", porque el mecanismo (SMTP, relay autorizado o autenticación integrada) sigue pendiente (§2.10). Se corrigieron `docs/diseno/02-modelo-datos.md` §3.5 y §3.6. También quedó registrado que la cuenta de referencia `seguros.fonprebel@prebel.com.co` es dato de configuración a validar con TI, nunca valor quemado en código.
L8 — 2026-11-06 — Corrección de un prerrequisito erróneo. Yo había escrito que había que instalar el SDK de .NET 8. El usuario señaló que en el proyecto BBVA no se pidió ningún SDK. Verificado en `Desktop/WTW/BBVA/2026_AVALUOS_BANCO_BBVA`: `global.json` declara `8.0.100` con `rollForward: latestMajor`, el csproj apunta a `net8.0-windows` con `WPF-UI 4.3.0`, y la máquina ya tiene el runtime `Microsoft.WindowsDesktop.App 8.0.33`. Conclusión: **no hay que instalar nada**; se copia el patrón de `global.json` y el SDK 10 compila. Se corrigió `docs/diseno/01-arquitectura.md` (D-01 y nueva sección §1.2 con el precedente verificado) y la nota de A1 en el checklist.

## Alias `T#` para delegación

El gate de writers exige referenciar la tarea como `T<n>`. El alias es la posición de la fila en la tabla de tareas, empezando en 1.

| Tarea | Alias | Tarea | Alias | Tarea | Alias |
|---|---|---|---|---|---|
| D1 | T1 | D2 | T2 | D3 | T3 |
| D4 | T4 | D5 | T5 | A1 | T6 |
| A2 | T7 | A3 | T8 | A4 | T9 |
| A5 | T10 | A6 | T11 | A7 | T12 |
| A8 | T13 | A9 | T14 | A10 | T15 |
| B1 | T16 | B2 | T17 | B3 | T18 |
| B4 | T19 | B5 | T20 | B6 | T21 |
| B7 | T22 | C1 | T23 | C2 | T24 |
| C3 | T25 | C4 | T26 |  |  |

L9 — 2026-11-06 — A1 implementado y verificado. Comandos observados: `dotnet build FonPrebel.sln -v q` → "Compilación correcta. 0 Advertencia(s) 0 Errores"; `dotnet test FonPrebel.sln --nologo` → "Con error: 0, Superado: 6, Omitido: 0, Total: 6"; arranque real de `FonPrebel.exe` → seguía en ejecución a los 7 segundos (se cerró desde el script). Hallazgos: el SDK 10 crea `.slnx` por defecto y **VS 2022 no lo abre**, así que la solución se generó con `--format sln` (D-03); `NavigationView` de WPF-UI 4.3.0 no acepta contenido hijo, así que la ventana es un `Grid` de dos columnas; `SelectedItem` tiene setter inaccesible y el resaltado inicial se logra con `IsActive = true`. Archivo de código más grande: 91 líneas, dentro del límite de 400.
