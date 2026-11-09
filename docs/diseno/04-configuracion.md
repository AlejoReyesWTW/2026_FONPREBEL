# D5 — Matriz de configuración y reglas de credenciales

Estado: propuesta de diseño. No hay nada implementado.
Fuente: `Specs/especificacion-fon-prebel-v0.13.md` §2.9, §2.9.1, §2.9.2, §2.10, §2.10.1, §2.10.2, Anexo D.
Regla que gobierna el documento: "**Ningún parámetro tiene un valor por defecto inventado**: los valores reales los entregan TI y el analista" (§2.9.1). Todo campo sin valor confirmado se guarda nulo y se muestra vacío.

## 1. Parámetros

La columna *Almacenamiento* respeta literalmente la tabla de §2.9.1, incluido el hecho de que el proxy y el tiempo de espera de envío viven en `AppConfiguration` y no en la configuración de correo.

### 1.1 Correo

| Parámetro | Almacenamiento | Obligatorio | Validación | Valor |
|---|---|---|---|---|
| Servidor | `EmailConfiguration.Host` | sí | no vacío | por confirmar con TI |
| Puerto | `EmailConfiguration.Port` | **sí, siempre**, con o sin proxy (§2.9.2 r8) | entero 1–65535 | por confirmar con TI |
| Seguridad de la conexión | `EmailConfiguration.Security` | sí | conjunto de valores **por definir** (§2.9.1) | por confirmar |
| Proxy activado (interruptor) | `AppConfiguration` clave `Mail.ProxyEnabled` | sí | booleano | según el entorno |
| Proxy (campo de texto) | `AppConfiguration` clave `Mail.ProxyText` | **sólo si el proxy está activado** | formato **por confirmar con TI** | por confirmar |
| Cuenta (usuario) | `EmailConfiguration.Account` | **depende del protocolo** | texto; sólo si hay autenticación con usuario | por confirmar |
| Remitente | `EmailConfiguration.SenderAddress` | sí | dirección de correo válida | por confirmar |
| Contraseña | `EmailConfiguration.PasswordCipher` | **depende del protocolo** | se cifra con DPAPI antes de guardar | se gestiona en la pantalla (§3) |
| Tiempo máximo de espera por envío | `AppConfiguration` clave `Mail.SendTimeoutSeconds` | sí | entero positivo | pendiente |

### 1.2 Servidor de archivos

| Parámetro | Almacenamiento | Obligatorio | Validación | Valor |
|---|---|---|---|---|
| Ruta 1 — consolidado anual | `AppConfiguration` clave `FileServer.Route1` | sí | ruta existente y alcanzable; archivo, no carpeta | pendiente |
| Ruta 2 — 13 archivos de póliza | `AppConfiguration` clave `FileServer.Route2` | sí | carpeta alcanzable | pendiente |
| Ruta 3 — archivo del analista | `AppConfiguration` clave `FileServer.Route3` | sí | archivo existente y escribible (la fase 2 guarda ahí) | pendiente |
| Ruta 4 — PDFs | `AppConfiguration` clave `FileServer.Route4` | sí | carpeta alcanzable y escribible | pendiente |

### 1.3 Conexiones y concurrencia

| Parámetro | Almacenamiento | Obligatorio | Validación | Valor |
|---|---|---|---|---|
| Intervalo de verificación automática | `AppConfiguration` clave `Connections.AutoCheckMinutes` | sí | entero positivo | **2 minutos** (§2.9.1, único valor confirmado) |
| Umbral de conexión lenta (ms) | `AppConfiguration` clave `Connections.SlowThresholdMs` | sí | entero positivo | pendiente |
| Trabajadores para PDFs y para correos | `AppConfiguration` claves `Workers.PdfCount` y `Workers.EmailCount` | sí | entero ≥ 1 | pendientes |

## 2. Reglas de obligatoriedad

1. El puerto del correo es obligatorio **siempre**, con o sin proxy (§2.9.2 r8).
2. Proxy apagado: el campo de texto queda **deshabilitado y vacío** y la aplicación se conecta directamente. Proxy encendido: el campo se habilita y pasa a ser obligatorio (§2.9.2 r7).
3. El proxy **no** lleva usuario ni contraseña: el analista indicó que no se necesita. Si en el futuro se exigiera, sería otro secreto cifrado con DPAPI y un cambio de versión posterior (§2.9.2 r9).
4. La aplicación se adapta a que el entorno tenga o no proxy **sin cambiar código** (§2.9.2 r7).
5. Si falta un parámetro obligatorio o es inválido, **la operación afectada no arranca** y se muestra un mensaje claro con el nombre del parámetro. La aplicación no asume valores (§2.9.2 r5).
6. Al guardar se validan campos obligatorios y formato — por ejemplo, que el puerto sea numérico — y se ofrece probar la conexión (§2.9.2 r2, §2.14).
7. Los parámetros que no son secretos se guardan sin cifrar. Sólo la contraseña se cifra con DPAPI (§2.9.2 r3, §2.10).
8. Los logs no registran contraseñas ni secretos al guardar o validar la configuración (§2.9.2 r6).

## 3. Credenciales (§2.10.1)

### 3.1 Estado normal

- El campo de contraseña se muestra enmascarado.
- Hay un botón de ojo para mostrar u ocultar lo que el usuario está tipeando.
- **La contraseña guardada nunca se muestra ni se precarga.** Si existe una credencial configurada, se indica ese hecho y se ofrece reemplazarla.
- Junto al campo hay un botón con icono de lápiz que entra en modo edición.

### 3.2 Modo edición

Tres campos obligatorios: **contraseña anterior**, **contraseña nueva**, **confirmación de la nueva**.

Orden de la operación, sin atajos:

1. Descifrar la credencial actual con DPAPI **en memoria** y compararla localmente con la contraseña anterior ingresada. Si no coincide, no se permite reemplazar nada.
2. Validar la contraseña nueva contra el **mecanismo real de autenticación** del servidor de correo. Si la validación falla, se conserva la credencial anterior y se informa el motivo **sin revelar secretos**.
3. Sólo si ambos pasos pasan, reemplazar el secreto cifrado. La operación es **atómica**: ante un fallo se conserva la credencial anterior.

Caso particular del spec: si la contraseña se cambió directamente en el correo antes de actualizarla en la aplicación, el analista ingresa como *anterior* el valor que la aplicación tiene guardado y como *nueva* la contraseña vigente. Si el mecanismo de correo no permite validar en ese momento, **no se debe afirmar que quedó validada** (§2.10.1).

### 3.3 Estados visuales

| Estado | Borde | Mensaje |
|---|---|---|
| Obligatorio vacío | rojo | nombre del campo y qué falta |
| Validando | spinner | — |
| Válido | verde | confirmación breve que luego se limpia |
| Inválido | rojo | motivo concreto sin datos sensibles |
| Guardado | neutro | confirmación; se limpian los indicadores temporales |
| Secreto no descifrable | rojo | el perfil de Windows cambió: hay que reingresar la credencial |

## 4. DPAPI y cambio de perfil

- Ámbito propuesto: `CurrentUser`. **Debe validarse con TI** (§2.10).
- Si cambia el usuario de Windows o el perfil deja de estar disponible, la credencial protegida puede no ser recuperable. La aplicación debe permitir **volver a configurarla sin perder el historial operativo** (§2.10).
- DPAPI protege la credencial, **no** cifra toda la base SQLite. Los permisos del archivo y la protección de los datos personales se evalúan por separado (§2.10).
- La contraseña real no se escribe en el spec, ni en documentos, código, repositorio, log o archivo de configuración. Ya existe una captura compartida cuyo secreto debe considerarse expuesto y conviene cambiar (§2.10.1).

## 5. Qué operación bloquea qué

| Operación | Requiere | No requiere |
|---|---|---|
| Fase 1 — Consolidación | Rutas 1 y 2, periodo | correo, proxy |
| Fase 2 — Cruce | Ruta 3 (lectura y escritura) | correo, proxy |
| Fase 3 — PDFs | Ruta 3, ruta 4, trabajadores de PDF | correo |
| Fase 4 — Correo | correo completo + credencial (si el protocolo la exige) + proxy (si está activo) + tiempo de espera | rutas 1 y 2 |
| Fase 5 — Informe | `Output.Folder` | correo |
| INICIAR (proceso completo) | todo lo anterior | — |

Regla del spec: "No bloquear fases que no requieran esa credencial si el diseño permite ejecutarlas de forma independiente" (§2.10.2).

## 6. Mensajes literales

| Situación | Texto |
|---|---|
| Falta una credencial para enviar correos | `"Por favor, configure las credenciales necesarias para el envío de correos"` |
| Configuración incompleta al intentar iniciar | `"Completa todos los campos obligatorios de Configuración para habilitar el inicio del proceso."` |
| Primer arranque sin configuración | Alerta con acceso directo a la sección Configuración; no se ejecuta nada que dependa de lo que falte (§2.9.2 r1) |

## 7. Qué no es configuración editable

- Las reglas de negocio y la estructura de las cinco fases.
- Las columnas a copiar de cada tipo de póliza y la llave del cruce: son reglas de negocio y su ubicación se define cuando se confirmen (§2.9.1, §5.7).
- Los datos de los jubilados: cédulas, correos, valores.
- Los logs, que van a archivo (§2.17), y los archivos Excel y PDF, que no van a SQLite (§2.6).

## 8. Pendientes que necesitan a TI o al analista

| Pendiente | Consecuencia |
|---|---|
| Valores reales de correo: servidor, puerto, seguridad, remitente | La fase 4 no se puede probar de verdad |
| Mecanismo de autenticación del banco (SMTP, relay, autenticación integrada) | Define si hay cuenta y contraseña, y si el bloque de credenciales aplica |
| Protocolo y formato del proxy | El interruptor existe, pero el formato del campo no se puede validar |
| Rutas 1 a 4 | Fases 1 a 3 no se pueden probar |
| Umbral de conexión lenta en ms | El punto amarillo no tiene umbral |
| Trabajadores de PDF y correo | Se usan valores conservadores **sólo en pruebas locales**, nunca como valor por defecto en producción |
| Validación del ámbito DPAPI con TI | Riesgo de credencial no recuperable en otro perfil |
