# Diccionario de la base de datos local

Estado: primera migración implementada (`InitialCreate`). Fuente: `docs/diseno/02-modelo-datos.md` §1–§4 y
§7. Este documento describe lo que existe en la base, no lo que falta: los valores marcados como
pendientes en el diseño siguen pendientes y no se completaron con valores inventados.

## 1. Ubicación y motor

| Tema | Valor |
|---|---|
| Archivo | `%LOCALAPPDATA%\FonPrebel\Database\FonPrebel.db` (S5) |
| Motor | SQLite |
| Acceso | EF Core 8 + `Microsoft.EntityFrameworkCore.Sqlite`; sin SQL escrito a mano en la aplicación (S3) |
| Esquema | Entidades C# + Fluent API en `src/FonPrebel/Infrastructure/Database/Configurations` |
| Contexto | `AppDbContext` creado siempre desde `IDbContextFactory<AppDbContext>`: uno por operación, nunca compartido entre hilos |
| Migraciones | `src/FonPrebel/Infrastructure/Database/Migrations`; se aplican al iniciar, nunca recrean la base ni borran historial |

## 2. Apertura de conexión

Cada conexión que EF abre pasa por `SqlitePragmaInterceptor`, que aplica la configuración del motor
antes de cualquier operación:

| Ajuste | Origen | Comportamiento actual |
|---|---|---|
| `journal_mode = WAL` | clave `Database.WalEnabled` y §2.6 del spec | Se activa salvo que la clave exista con el valor `false`. Es persistente en el archivo |
| `busy_timeout` | clave `Database.BusyTimeoutMs` (§5.4, pendiente) | Si la clave tiene un entero mayor o igual a cero se envía tal cual; si no existe o está nula **no se envía nada** y el motor conserva su valor por omisión, con una advertencia en `execution.log` |
| Claves foráneas | `DatabaseSetup` | Se activan en la cadena de conexión (`Foreign Keys=True`) |

Las claves de `AppConfiguration` que gobiernan estos ajustes se leen una vez al iniciar, después de
aplicar las migraciones. Las conexiones abiertas antes de esa lectura usan los valores por omisión
(WAL activado, tiempo de espera pendiente).

## 3. Convenciones del esquema

| Tema | Implementación |
|---|---|
| Fechas | `TEXT` con ISO-8601 en UTC (`DateTime.ToUniversalTime()` y formato de ida y vuelta); se convierten a hora local sólo al mostrarse |
| Enumeraciones | `TEXT` con el nombre del valor, para que la base sea legible |
| Claves primarias | `INTEGER PRIMARY KEY AUTOINCREMENT` técnicas (`AppConfiguration` usa su clave de texto) |
| Identificadores de negocio | Restricciones `UNIQUE` explícitas (ver §10) |
| Borrado | Todas las claves foráneas son `ON DELETE RESTRICT`: el historial no se borra encadenado (§2.8 reglas 8 y 9) |

## 4. Relaciones

```text
AppConfiguration            (clave/valor, una fila por parámetro, sin relaciones)

EmailConfiguration          (una sola fila: configuración de correo + secreto DPAPI)

Batch  1───n  BatchExecution  1───n  PhaseExecution
  │                  │
  │                  └──n  ExecutionInputFingerprint
  │
  └──n  BatchRecipient

BatchExecution  n───1  BatchExecution   (ResumedFromExecutionId: reanudación)
```

## 5. `Batch` — lote

| Columna | Tipo | Nulo | Propósito |
|---|---|---|---|
| `Id` | INTEGER | no | Clave primaria técnica, autoincremental |
| `Code` | TEXT | no | Identificador único y legible del lote (§2.8 regla 1). El **formato está pendiente** (§5.3) |
| `PeriodYear` | INTEGER | no | Año del periodo |
| `PeriodMonth` | INTEGER | no | Mes del periodo, de 1 a 12 |
| `Status` | TEXT | no | `BatchStatus` (§4.1 del diseño) |
| `CreatedAtUtc` | TEXT | no | Momento de creación, ISO-8601 UTC |
| `CreatedBy` | TEXT | no | Usuario de Windows que abrió el lote |
| `Notes` | TEXT | sí | Observación libre del analista; no es una columna del Excel |

Índices: `UNIQUE(Code)`, `INDEX(PeriodYear, PeriodMonth)`, `INDEX(Status)`.
Un lote nunca se borra ni se sobrescribe; una reanudación crea una `BatchExecution` nueva.

## 6. `BatchExecution` — ejecución o reanudación

| Columna | Tipo | Nulo | Propósito |
|---|---|---|---|
| `Id` | INTEGER | no | Clave primaria técnica |
| `BatchId` | INTEGER | no | FK → `Batch.Id` |
| `Number` | INTEGER | no | Número de ejecución dentro del lote: 1, 2, 3… |
| `Status` | TEXT | no | `BatchStatus` (§4.1) |
| `StartedAtUtc` | TEXT | no | Inicio de la ejecución |
| `FinishedAtUtc` | TEXT | sí | Fin; nulo mientras corre |
| `LastCheckpointAtUtc` | TEXT | sí | Última confirmación persistida como punto de control (Anexo F.1) |
| `ResumedFromExecutionId` | INTEGER | sí | FK → `BatchExecution.Id` de la ejecución anterior, cuando esta fila es una reanudación |

Índices: `UNIQUE(BatchId, Number)`, `INDEX(ResumedFromExecutionId)`.
Reanudar **no** crea un `Batch` nuevo (§2.8 regla 5).

## 7. `PhaseExecution` — estado por fase

| Columna | Tipo | Nulo | Propósito |
|---|---|---|---|
| `Id` | INTEGER | no | Clave primaria técnica |
| `BatchExecutionId` | INTEGER | no | FK → `BatchExecution.Id` |
| `Phase` | INTEGER | no | Número de fase, de 1 a 5 |
| `Status` | TEXT | no | `PhaseStatus` (§4.2) |
| `StartedAtUtc` | TEXT | sí | Inicio de la fase |
| `FinishedAtUtc` | TEXT | sí | Fin de la fase |
| `ProcessedCount` | INTEGER | no | Avance de la fase; arranca en 0 |
| `TotalCount` | INTEGER | sí | Universo de la fase; nulo hasta conocerlo |
| `LastErrorCode` | TEXT | sí | Código estable del último error, para métricas |
| `LastErrorMessage` | TEXT | sí | Mensaje del último error para la interfaz y `errors.log`; sin secretos ni datos sensibles |

Índices: `UNIQUE(BatchExecutionId, Phase)`.

## 8. `BatchRecipient` — jubilado dentro del lote

| Columna | Tipo | Nulo | Propósito |
|---|---|---|---|
| `Id` | INTEGER | no | Clave primaria técnica |
| `BatchId` | INTEGER | no | FK → `Batch.Id` |
| `Cedula` | TEXT | no | `CEDULA ASOCIADO`, normalizada sin puntos para comparar. El formato de contraseña del PDF está **pendiente** (§2.11) |
| `FullName` | TEXT | sí | `NOMBRE ASOCIADO` |
| `Email` | TEXT | sí | `CORREO ELECTRÓNICO`. Nulo o vacío = no notificable |
| `PdfStatus` | TEXT | no | `PdfStatus` (§4.3) |
| `PdfPath` | TEXT | sí | Ruta del PDF generado. El nombre del archivo está **pendiente** |
| `SendStatus` | TEXT | no | `SendStatus` (§4.4) |
| `SendAttemptCount` | INTEGER | no | Intentos de envío realizados; la política de reintentos está **pendiente** (§5.2) |
| `LastAttemptAtUtc` | TEXT | sí | Momento del último intento |
| `ConfirmedAtUtc` | TEXT | sí | Sólo se escribe con confirmación del envío (§2.8 regla 12) |
| `LastErrorCode` | TEXT | sí | Código estable del último error |
| `LastErrorMessage` | TEXT | sí | Mensaje del último error; sin secretos |
| `UpdatedAtUtc` | TEXT | no | Última actualización del registro |

Índices: `UNIQUE(BatchId, Cedula)`, `INDEX(BatchId, SendStatus)`, `INDEX(BatchId, PdfStatus)`.
No existe columna `CELULAR`: su uso (WhatsApp) está pendiente (§Anexo B.5) y no se inventa.

## 9. `EmailConfiguration` — correo y secreto

Una sola fila, garantizada por la restricción `CK_EmailConfiguration_SingleRow` (`CHECK (Id = 1)`).
Todos los valores arrancan nulos porque están pendientes de confirmar (§2.9.1).

| Columna | Tipo | Nulo | Propósito |
|---|---|---|---|
| `Id` | INTEGER | no | Clave primaria; la tabla sólo admite el valor 1 |
| `Host` | TEXT | sí | Servidor de correo |
| `Port` | INTEGER | sí | Puerto |
| `Security` | TEXT | sí | Seguridad de la conexión; valores previstos `None`, `StartTls`, `SslOnConnect`. El valor real está **pendiente** |
| `Account` | TEXT | sí | Cuenta de correo; queda nula si el mecanismo es autenticación integrada (§2.10) |
| `SenderAddress` | TEXT | sí | Remitente. Se configura; nunca se escribe en el código |
| `PasswordCipher` | BLOB | sí | Contraseña cifrada con DPAPI `CurrentUser`: el único dato cifrado de la base (§2.10) |
| `PasswordUpdatedAtUtc` | TEXT | sí | Momento del último cambio del secreto |
| `UpdatedAtUtc` | TEXT | no | Última actualización del registro |

No incluye proxy, tiempo de espera por envío ni un indicador de autenticación: §2.9.1 ubica los dos
primeros en `AppConfiguration` y el mecanismo de autenticación sigue pendiente (§2.10).

## 10. `AppConfiguration` — parámetros generales

Modelo clave/valor a propósito: la lista de parámetros está **pendiente de confirmación** (§2.9.1).

| Columna | Tipo | Nulo | Propósito |
|---|---|---|---|
| `Key` | TEXT | no | Clave primaria del parámetro |
| `Value` | TEXT | sí | Valor; **nulo significa "pendiente de configurar"** y la interfaz lo muestra vacío |
| `UpdatedAtUtc` | TEXT | no | Última actualización |
| `UpdatedBy` | TEXT | sí | Usuario que actualizó el parámetro |

Claves que el diseño registra en §3.6. La tabla arranca **sin filas**: la pantalla Configuración (A5)
es la que las crea con estos valores iniciales.

| Clave | Valor inicial documentado | Uso actual |
|---|---|---|
| `FileServer.Route1` | nulo | Ruta del consolidado anual (§2.12) |
| `FileServer.Route2` | nulo | Ruta de los 16 archivos de póliza |
| `FileServer.Route3` | nulo | Ruta del archivo del analista |
| `FileServer.Route4` | nulo | Ruta de los PDFs |
| `Output.Folder` | `%LOCALAPPDATA%\FonPrebel\Salida` | Carpeta de informes (§2.6) |
| `Connections.AutoCheckMinutes` | `2` | Único valor que el spec confirma |
| `Mail.ProxyEnabled` | nulo | Interruptor de proxy (§2.9.1) |
| `Mail.ProxyText` | nulo | Texto del proxy; obligatorio sólo si el proxy está activado |
| `Mail.SendTimeoutSeconds` | nulo | Tiempo máximo de espera por envío (§2.9.1) |
| `Connections.SlowThresholdMs` | nulo | Umbral de conexión lenta (§2.14) |
| `Database.BusyTimeoutMs` | nulo | Tiempo de espera ante bloqueo (§5.4). **Leído hoy**, ver §2 |
| `Database.WalEnabled` | `true` | Modo WAL (§2.6). **Leído hoy**: si la clave no existe se asume `true` |
| `Workers.PdfCount` | nulo | Paralelismo de PDFs (§2.15) |
| `Workers.EmailCount` | nulo | Paralelismo de correo; el envío es secuencial |

## 11. `ExecutionInputFingerprint` — huellas de archivos de entrada

Tabla que no estaba en la lista candidata del spec (explícitamente preliminar en §2.7) pero que el
Anexo F.4 necesita para comparar las entradas al reanudar.

| Columna | Tipo | Nulo | Propósito |
|---|---|---|---|
| `Id` | INTEGER | no | Clave primaria técnica |
| `BatchExecutionId` | INTEGER | no | FK → `BatchExecution.Id` |
| `Role` | TEXT | no | Papel del archivo: `Consolidated`, `AnalystFile` o `PolicySource` |
| `FilePath` | TEXT | no | Ruta completa usada en la ejecución |
| `FileSizeBytes` | INTEGER | no | Tamaño en bytes |
| `FileModifiedAtUtc` | TEXT | no | Última modificación del archivo |
| `Sha256` | TEXT | no | Hash SHA-256 del contenido |
| `CapturedAtUtc` | TEXT | no | Momento de captura de la huella |

Índices: `INDEX(BatchExecutionId, Role)`.

## 12. Enumeraciones

| Enumeración | Valores |
|---|---|
| `BatchStatus` | `LISTO`, `EJECUTANDO`, `PAUSADO_POR_INCONSISTENCIA`, `VALIDANDO_RECUPERACION`, `LISTO_PARA_REANUDAR`, `COMPLETADO`, `CANCELADO` |
| `PhaseStatus` | `PENDIENTE`, `EN_EJECUCION`, `ESPERANDO_CONFIRMACION`, `COMPLETADA`, `FALLIDA`, `CANCELADA` |
| `PdfStatus` | `PENDIENTE`, `GENERADO`, `FALLIDO` |
| `SendStatus` | `PENDIENTE`, `ENVIANDO`, `ENVIADO`, `FALLIDO`, `INCIERTO` |

Todos los nombres son los del diseño; la lista exacta de estados sigue marcada como pendiente en §5.3
del spec. `ESPERANDO_CONFIRMACION` y `ENVIANDO` son propuestas de diseño señaladas como tales.

## 13. Índices y restricciones

| Tabla | Restricción |
|---|---|
| `Batch` | `IX_Batch_Code` UNIQUE; `IX_Batch_PeriodYear_PeriodMonth`; `IX_Batch_Status`; PK `Id` |
| `BatchExecution` | `IX_BatchExecution_BatchId_Number` UNIQUE; `IX_BatchExecution_ResumedFromExecutionId`; FK a `Batch` y a `BatchExecution` con `RESTRICT` |
| `PhaseExecution` | `IX_PhaseExecution_BatchExecutionId_Phase` UNIQUE; FK a `BatchExecution` con `RESTRICT` |
| `BatchRecipient` | `IX_BatchRecipient_BatchId_Cedula` UNIQUE; `IX_BatchRecipient_BatchId_SendStatus`; `IX_BatchRecipient_BatchId_PdfStatus`; FK a `Batch` con `RESTRICT` |
| `EmailConfiguration` | PK `Id`; `CK_EmailConfiguration_SingleRow` (`CHECK ("Id" = 1)`) |
| `AppConfiguration` | PK `Key` |
| `ExecutionInputFingerprint` | `IX_ExecutionInputFingerprint_BatchExecutionId_Role`; FK a `BatchExecution` con `RESTRICT` |

## 14. Aplicación y evolución del esquema

- `DatabaseStartupService` aplica las migraciones pendientes una sola vez al iniciar, con la bitácora
  ya abierta. Si falla, el error queda en `errors.log`, la aplicación muestra el detalle al analista y
  la base **no se borra ni se recrea**.
- El manifiesto local `.config/dotnet-tools.json` fija `dotnet-ef` 8.0.33. Una migración nueva se genera
  con:

```powershell
dotnet dotnet-ef migrations add <Nombre> --project src/FonPrebel --startup-project src/FonPrebel --output-dir Infrastructure/Database/Migrations
```

## 15. Pendientes que no se completaron

| Falta | Columna o tabla afectada | Quién responde |
|---|---|---|
| Formato del identificador de lote | `Batch.Code` | funcional (§5.3) |
| Estados exactos de lote, ejecución, fase y destinatario | §12 | funcional (§5.3) |
| Nombre del PDF y del informe de la fase 5 | `BatchRecipient.PdfPath`, salida | funcional (§3, §5.7) |
| Formato de la cédula como contraseña | `BatchRecipient.Cedula` | funcional / banco (§2.11) |
| Valores de `busy_timeout` y de WAL más allá del interruptor | §2 | TI (§5.4) |
| Contenido real de la configuración de correo y de los parámetros | `EmailConfiguration`, `AppConfiguration` | TI / analista (§2.9.1, §5.1) |
