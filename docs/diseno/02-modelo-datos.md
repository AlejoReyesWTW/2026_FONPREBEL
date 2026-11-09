# D2 + D4 — Modelo de datos, estados y recuperación

Estado: diseño propuesto para aprobación. No hay base creada ni migración escrita.
Fuente: `Specs/especificacion-fon-prebel-v0.12.md` §2.6, §2.7, §2.8, §2.9, §2.10, §2.17, §5.3, §5.4, Anexo C, Anexo F.
Regla que gobierna este documento: el spec exige que el esquema se **diseñe y documente antes de implementarlo** (§2.7), y prohíbe inventar campos de negocio (§2.7, §6 regla 2). Por eso cada columna de negocio aparece con la sección que la nombra, o marcada como técnica.

## 1. Convenciones

| Tema | Decisión |
|---|---|
| Motor | SQLite con EF Core 8 + proveedor `Microsoft.EntityFrameworkCore.Sqlite` |
| Definición del esquema | Entidades C# + Fluent API en `Infrastructure/Database/Configurations`. Sin SQL en texto (§2.7) |
| Contexto | `IDbContextFactory<AppDbContext>`; un contexto por operación, nunca compartido entre hilos (§2.6) |
| Fechas | UTC en ISO-8601 (`TEXT`) en la base; se convierten a hora local sólo al mostrarse |
| Enumeraciones | Se guardan como `TEXT` con el nombre del valor, para que la base sea legible al revisarla |
| Claves | `INTEGER PRIMARY KEY AUTOINCREMENT` técnicas; los identificadores de negocio son `UNIQUE` |
| Migraciones | Versionadas, aplicadas al iniciar; nunca recrean la base ni borran historial (§2.7) |
| Concurrencia | Modo WAL y *busy timeout* parametrizado. **Los valores concretos están pendientes** (§2.6, §5.4) |

## 2. Relaciones

```text
AppConfiguration            (clave/valor, una fila por parámetro)

EmailConfiguration          (una sola fila: configuración de correo + secreto DPAPI)

Batch  1───n  BatchExecution  1───n  PhaseExecution
  │                  │
  │                  └──n  ExecutionInputFingerprint
  │
  └──n  BatchRecipient
```

## 3. Tablas

### 3.1 `Batch` — lote (§2.8)

| Columna | Tipo | Nulo | Origen / nota |
|---|---|---|---|
| `Id` | INTEGER | no | PK técnica |
| `Code` | TEXT | no | Identificador único y legible (§2.8 regla 1). Formato **pendiente** (§5.3); `2026-11-002` es sólo ilustrativo (§2.8 regla 3) |
| `PeriodYear` | INTEGER | no | Periodo del lote (§2.8) |
| `PeriodMonth` | INTEGER | no | 1–12. Determinado por el mes de la hoja de la fase 1 |
| `Status` | TEXT | no | `BatchStatus` (§4.1) |
| `CreatedAtUtc` | TEXT | no | Técnica |
| `CreatedBy` | TEXT | no | Usuario de Windows que abrió el lote |
| `Notes` | TEXT | sí | Observación libre del analista. No es una columna del Excel |

Índices: `UNIQUE(Code)`, `INDEX(PeriodYear, PeriodMonth)`, `INDEX(Status)`.
Regla: nunca se borra ni se sobrescribe un lote anterior (§2.8 reglas 8 y 9).

### 3.2 `BatchExecution` — ejecución o reanudación (§2.8)

| Columna | Tipo | Nulo | Origen / nota |
|---|---|---|---|
| `Id` | INTEGER | no | PK técnica |
| `BatchId` | INTEGER | no | FK → `Batch` |
| `Number` | INTEGER | no | 1, 2, 3… por lote. `UNIQUE(BatchId, Number)` |
| `Status` | TEXT | no | `BatchStatus` (§4.1) |
| `StartedAtUtc` | TEXT | no | Técnica |
| `FinishedAtUtc` | TEXT | sí | Nulo mientras corre |
| `LastCheckpointAtUtc` | TEXT | sí | Última confirmación persistida (§Anexo F.1) |
| `ResumedFromExecutionId` | INTEGER | sí | FK a la ejecución anterior, cuando esta ejecución es una reanudación |

Reanudar **no** crea un `Batch` nuevo (§2.8 regla 5); crea una fila nueva en `BatchExecution` del mismo lote.

### 3.3 `PhaseExecution` — estado por fase (§2.8)

| Columna | Tipo | Nulo | Origen / nota |
|---|---|---|---|
| `Id` | INTEGER | no | PK técnica |
| `BatchExecutionId` | INTEGER | no | FK → `BatchExecution` |
| `Phase` | INTEGER | no | 1 a 5. `UNIQUE(BatchExecutionId, Phase)` |
| `Status` | TEXT | no | `PhaseStatus` (§4.2) |
| `StartedAtUtc` | TEXT | sí | |
| `FinishedAtUtc` | TEXT | sí | |
| `ProcessedCount` | INTEGER | no | Avance de la fase; por defecto 0 |
| `TotalCount` | INTEGER | sí | Nulo hasta conocer el universo de la fase |
| `LastErrorCode` | TEXT | sí | Código estable para métricas |
| `LastErrorMessage` | TEXT | sí | Mensaje para la UI y `errors.log`. **Sin secretos ni datos sensibles** (§2.17) |

### 3.4 `BatchRecipient` — jubilado dentro del lote (§2.8)

| Columna | Tipo | Nulo | Origen / nota |
|---|---|---|---|
| `Id` | INTEGER | no | PK técnica |
| `BatchId` | INTEGER | no | FK → `Batch`. `UNIQUE(BatchId, Cedula)` |
| `Cedula` | TEXT | no | `CEDULA ASOCIADO` (Anexo A y B). Se normaliza sin puntos para comparar; el formato de contraseña del PDF está **pendiente** (§2.11) |
| `FullName` | TEXT | sí | `NOMBRE ASOCIADO` (Anexo B) |
| `Email` | TEXT | sí | `CORREO ELECTRÓNICO` (Anexo B). Nulo o vacío = no notificable (§3 fase 4) |
| `PdfStatus` | TEXT | no | `PdfStatus` (§4.3) |
| `PdfPath` | TEXT | sí | Ruta del PDF en la ruta 4 (servidor). Nombre del archivo **pendiente** |
| `SendStatus` | TEXT | no | `SendStatus` (§4.4) |
| `SendAttemptCount` | INTEGER | no | Reintentos controlados (política **pendiente**, §5.2) |
| `LastAttemptAtUtc` | TEXT | sí | |
| `ConfirmedAtUtc` | TEXT | sí | Sólo se escribe con confirmación del mecanismo de envío (§2.8 regla 12) |
| `LastErrorCode` | TEXT | sí | |
| `LastErrorMessage` | TEXT | sí | |
| `UpdatedAtUtc` | TEXT | no | |

Índices: `UNIQUE(BatchId, Cedula)`, `INDEX(BatchId, SendStatus)`, `INDEX(BatchId, PdfStatus)`.
Deliberadamente **no** incluye `CELULAR`: el spec lo menciona como posible WhatsApp y su uso está pendiente (§Anexo B.5). No se crea la columna hasta que se defina.

### 3.5 `EmailConfiguration` — correo y secreto (§2.9, §2.10)

Una sola fila. Todos los valores están **pendientes de confirmar** (§2.9.1): las columnas existen, el contenido arranca nulo.

| Columna | Tipo | Nulo | Origen / nota |
|---|---|---|---|
| `Id` | INTEGER | no | PK; restricción `CHECK (Id = 1)` |
| `Host` | TEXT | sí | Servidor de correo |
| `Port` | INTEGER | sí | Puerto |
| `Security` | TEXT | sí | Seguridad de conexión (`None`, `StartTls`, `SslOnConnect`). El valor real **pendiente** |
| `Account` | TEXT | sí | Cuenta (usuario) (§2.9.1). El mecanismo de autenticación (SMTP, relay autorizado o autenticación integrada) **está pendiente** (§2.10): si el banco usa autenticación integrada, este campo queda nulo |
| `SenderAddress` | TEXT | sí | Remitente (§2.9.1). Referencia a validar con TI: `seguros.fonprebel@prebel.com.co` (§2.10.1). **No se escribe en el código**, se configura |
| `PasswordCipher` | BLOB | sí | Contraseña cifrada con DPAPI `CurrentUser` (§2.10). **Es el único dato cifrado**, tal como define el spec |
| `PasswordUpdatedAtUtc` | TEXT | sí | Necesario para saber si el secreto puede descifrarse en este perfil de Windows |
| `UpdatedAtUtc` | TEXT | no | |

No incluye `RequiresAuth`: si el banco usa autenticación integrada no hay cuenta ni secreto, y agregar la columna obligaría a un valor inventado (§2.10). Tampoco incluye proxy ni tiempo de espera: §2.9.1 los ubica explícitamente en `AppConfiguration`.

### 3.6 `AppConfiguration` — parámetros generales (§2.9)

Modelo clave/valor a propósito: la lista de parámetros está **pendiente de confirmación** (§2.9.1) y "Ningún parámetro tiene un valor por defecto inventado". Con clave/valor se agregan parámetros sin migración y sin inventar columnas.

| Columna | Tipo | Nulo | Nota |
|---|---|---|---|
| `Key` | TEXT | no | PK. Claves registradas abajo |
| `Value` | TEXT | sí | Nulo = pendiente de configurar. La UI lo muestra vacío, nunca con un valor inventado |
| `UpdatedAtUtc` | TEXT | no | |
| `UpdatedBy` | TEXT | sí | |

Claves registradas (valor inicial **nulo** salvo donde el spec fija el dato):

| Clave | Origen | Valor inicial |
|---|---|---|
| `FileServer.Route1` | §2.12 ruta 1 (consolidado anual) | nulo |
| `FileServer.Route2` | §2.12 ruta 2 (13 archivos de póliza) | nulo |
| `FileServer.Route3` | §2.12 ruta 3 (archivo del analista) | nulo |
| `FileServer.Route4` | §2.12 ruta 4 (PDFs) | nulo |
| `Output.Folder` | §2.6 `Salida/` | `%LOCALAPPDATA%\FonPrebel\Salida` (ruta de datos local, no valor de negocio) |
| `Connections.AutoCheckMinutes` | §2.9.1 y §2.14 | `2` — **único valor que el spec confirma** |
| `Mail.ProxyEnabled` | §2.9.1 (proxy, interruptor) | nulo. Apagado: el campo de texto queda deshabilitado y vacío |
| `Mail.ProxyText` | §2.9.1 (proxy, campo de texto) | nulo. Obligatorio **sólo** si el proxy está activado; el formato lo confirma TI |
| `Mail.SendTimeoutSeconds` | §2.9.1 (tiempo máximo de espera por envío) | nulo |
| `Connections.SlowThresholdMs` | §2.14 | nulo (umbral pendiente) |
| `Database.BusyTimeoutMs` | §2.6, §5.4 | nulo (pendiente) |
| `Database.WalEnabled` | §2.6 | `true` (el spec ordena activar WAL; el valor del timeout es el pendiente) |
| `Workers.PdfCount` | §2.15 | nulo (paralelismo pendiente) |
| `Workers.EmailCount` | §2.15 | nulo; el envío es secuencial, uno por uno |

Los tres parámetros de correo que el spec ubica en `AppConfiguration` (proxy activado, texto del proxy, tiempo máximo de espera por envío) van acá y no en `EmailConfiguration`, tal como indica la tabla de §2.9.1.

No se guardan acá ni la contraseña ni los logs ni los archivos Excel/PDF (§2.6).

### 3.7 `ExecutionInputFingerprint` — huellas de archivos de entrada (Anexo F.4)

Tabla no incluida en la lista candidata del spec, que es explícitamente preliminar (§2.7): Anexo F exige recalcular y comparar la huella de los archivos de entrada al reanudar, y eso necesita persistencia.

| Columna | Tipo | Nulo | Nota |
|---|---|---|---|
| `Id` | INTEGER | no | PK |
| `BatchExecutionId` | INTEGER | no | FK → `BatchExecution` |
| `Role` | TEXT | no | Papel del archivo (`Consolidated`, `AnalystFile`, `PolicySource`) |
| `FilePath` | TEXT | no | Ruta completa usada en la ejecución |
| `FileSizeBytes` | INTEGER | no | |
| `FileModifiedAtUtc` | TEXT | no | |
| `Sha256` | TEXT | no | Hash del contenido (Anexo F.4) |
| `CapturedAtUtc` | TEXT | no | |

Índice: `INDEX(BatchExecutionId, Role)`.

## 4. Enumeraciones

Los nombres son los del spec cuando el spec los nombra; los agregados se marcan como propuesta.

### 4.1 `BatchStatus`
`LISTO`, `EJECUTANDO`, `PAUSADO_POR_INCONSISTENCIA`, `VALIDANDO_RECUPERACION`, `LISTO_PARA_REANUDAR`, `COMPLETADO`, `CANCELADO`.
Todos provienen literalmente de Anexo F.3. La lista exacta sigue marcada como pendiente en §5.3; si el banco agrega o quita estados, se ajusta acá y no se inventa en el código.

### 4.2 `PhaseStatus`
`PENDIENTE`, `EN_EJECUCION`, `ESPERANDO_CONFIRMACION`, `COMPLETADA`, `FALLIDA`, `CANCELADA`.
`ESPERANDO_CONFIRMACION` es **propuesta de diseño**: el spec exige que el analista confirme entre fases (§1, §3 fase 1 y 2, §4) pero no le pone nombre al estado. Se marca para confirmación con el funcional.

### 4.3 `PdfStatus`
`PENDIENTE`, `GENERADO`, `FALLIDO`. El PDF confirmado nunca se regenera en una reanudación (§2.16).

### 4.4 `SendStatus`
`PENDIENTE`, `ENVIANDO`, `ENVIADO`, `FALLIDO`, `INCIERTO`.
`PENDIENTE`/`ENVIADO`/`FALLIDO` son del spec (§2.8 regla 11). `INCIERTO` representa el resultado desconocido cuando se pierde la conexión (§2.8 regla 11) y **nunca** se muestra como enviado (§2.8 regla 12). `ENVIANDO` es propuesta técnica: sin ese estado intermedio, un corte de energía a mitad de envío no se distingue de "todavía no se intentó".

## 5. Reanudación e idempotencia (Anexo F)

Al abrir la aplicación:

1. Buscar el último `BatchExecution` con estado en `{EJECUTANDO, PAUSADO_POR_INCONSISTENCIA, LISTO_PARA_REANUDAR}`.
2. Pasar ese lote a `VALIDANDO_RECUPERACION`.
3. Recalcular `Sha256` de cada archivo registrado en `ExecutionInputFingerprint` y comparar tamaño y fecha.
4. Si alguna huella difiere: `PAUSADO_POR_INCONSISTENCIA`, registrarlo en `errors.log` y mostrar la alerta crítica del Anexo E. **No se reanuda automáticamente** y no se reescribe el archivo del analista.
5. Si todas coinciden: `LISTO_PARA_REANUDAR` y la UI ofrece continuar el mismo lote (§2.8 regla 4).
6. Al reanudar se crea una `BatchExecution` nueva con `ResumedFromExecutionId`, no un lote nuevo (§2.8 regla 5).

Reglas de idempotencia, una por artefacto:

| Artefacto | Regla |
|---|---|
| Hoja del mes (fase 1) | Si ya existe para el lote, no se recrea ni se duplica (§Anexo F.9) |
| Archivo del analista (fase 2) | Se escribe sobre la ruta 3 sólo con confirmación del analista; una reanudación no reescribe valores ya completados |
| PDF (fase 3) | Si `PdfStatus = GENERADO` y el archivo existe, no se regenera (§2.16) |
| Correo (fase 4) | Si `SendStatus = ENVIADO`, no se reenvía (§2.8 regla 6) |
| Informe (fase 5) | Sólo se genera cuando las fases 2, 3 y 4 están en estado terminal |

Resultados temporales incompletos se descartan; los válidos se conservan (Anexo F.6).

## 6. Escritura concurrente

- Los workers de PDF y correo escriben estado en paralelo (§2.6, §2.16). Cada uno usa su propio contexto, creado con la factoría.
- Cada confirmación de un destinatario es una transacción corta: una fila, no un lote de filas. Así una caída pierde como máximo el trabajo de un jubilado y no obliga a repetir todo.
- WAL y *busy timeout* evitan bloqueos entre escrituras concurrentes. Sus valores se configuran en `AppConfiguration` y hoy están pendientes.

## 7. Lo que falta para poder escribir la primera migración

| Falta | Bloquea | Quién responde |
|---|---|---|
| Rutas reales 1–4 | poblar `AppConfiguration` | TI / analista (§5.1) |
| Formato del identificador de lote | `Batch.Code` | funcional (§5.3) |
| Estados exactos de lote, ejecución, fase y destinatario | §4 completo | funcional (§5.3) |
| Nombre del PDF y del informe de la fase 5 | `PdfPath`, salida | funcional (§3, §5.7) |
| Formato de la cédula como contraseña (con o sin puntos) | `BatchRecipient.Cedula` normalizada | funcional / banco (§2.11) |
| Valores de WAL y *busy timeout* | §6 | TI (§5.4) |

Nada de esto impide implementar las entidades, sus configuraciones Fluent y la primera migración: las columnas ya están definidas y los valores nulos son válidos. Lo que no se puede hacer es **confirmar** el esquema hasta que el funcional y TI respondan.
