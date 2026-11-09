# Fon Prebel - Manejo de Pólizas de Jubilados

**Versión 0.11 -- Reducción de los insumos de la ruta 2: de 16 a 13 archivos de póliza**\
**Estado:** Borrador en construcción.\
**Documento base:** Versión 0.10.\
**Fecha de actualización:** 7 de noviembre de 2026.

> Este documento conserva los requisitos funcionales de la versión 0.2 e
> incorpora las decisiones acordadas durante la revisión. Los puntos no
> definidos se mantienen explícitamente como pendientes. No deben ser
> completados mediante suposiciones por el equipo de desarrollo ni por
> agentes de codificación.

------------------------------------------------------------------------

## 1. Resumen

Aplicativo de escritorio para Fon Prebel que automatiza el procesamiento
mensual de pólizas de jubilados: consolida información de 13 tipos de
póliza, completa el archivo que prepara el analista, genera un PDF por
jubilado, lo envía por correo electrónico y entrega un informe en Excel
de quiénes fueron notificados y quiénes no.

No es un bot desatendido: un analista opera la aplicación y confirma los
pasos entre fases.

La aplicación se ejecutará en una sola máquina: el equipo del analista.
Los archivos de trabajo (Excel y PDF) estarán en un servidor de archivos
compartido. La base de datos SQLite será local al equipo y se utilizará
para configuración, historial y seguimiento de lotes y ejecuciones. Los
logs permanecerán en archivos `.log`, no en SQLite. El acceso a datos
se hace con un ORM, sin SQL en texto dentro del código. En una etapa
final se agregará un inicio de sesión validado contra una base de datos
central de la empresa (apartado 2.23).

## 2. Especificación técnica

### 2.1 Tecnología

-   Lenguaje y plataforma: C# .NET.
-   Interfaz: WPF UI original de Lepoco ([repositorio
    oficial](https://github.com/lepoco/wpfui), paquete NuGet
    [WPF-UI](https://www.nuget.org/packages/WPF-UI/)), para que la
    aplicación tenga el aspecto Fluent de una aplicación moderna de
    Windows. No se usan forks ni paquetes de terceros con nombre
    parecido.
-   Patrón de presentación: MVVM (Model-View-ViewModel), aprovechando el
    binding, los comandos y la separación entre interfaz y lógica.
-   Arquitectura: capas sencillas inspiradas en Clean Architecture, sin
    implementar complejidad innecesaria.
-   Paradigma: programación orientada a objetos.
-   Diseño: aplicar SOLID de manera pragmática. No crear interfaces,
    capas, proyectos o patrones si no aportan una responsabilidad o
    desacoplamiento real.
-   Persistencia local: SQLite, integrada mediante una
    biblioteca/paquete NuGet. No requiere instalar ni administrar un
    servidor SQL ni SQL Server Management Studio.
-   ORM: Entity Framework Core, con el proveedor SQLite para la base
    local. Las consultas se escriben en LINQ; no se escribe SQL en
    texto dentro del código (apartado 2.7). La versión de EF Core se
    alinea con la versión de .NET elegida.
-   Base central del inicio de sesión (etapa final): SQL Server,
    accedida también con EF Core mediante un contexto y un proveedor
    independientes de los de la base local (apartado 2.23).
-   Protección de credenciales: DPAPI de Windows para cifrar los
    secretos antes de almacenarlos en SQLite. No almacenar contraseñas
    en texto plano.
-   Logs: archivos `.log` diarios, separados de SQLite.
-   Configuración: se administra desde la pantalla de Configuración y
    se guarda en SQLite (apartado 2.9). No se usa archivo `.env`.
-   Ejecución: una sola máquina, la del analista.
-   Despliegue: se evaluará una publicación autocontenida para Windows y
    un instalador autorizado por TI. La modalidad final depende de las
    políticas del banco.

### 2.2 Arquitectura y responsabilidades

La solución se organizará en capas con responsabilidades claras:

-   **Presentation:** interfaz WPF, Views, ViewModels, comandos y
    recursos visuales. No debe contener lógica de negocio ni acceder
    directamente a Excel, PDF, correo o SQLite.
-   **Application:** casos de uso y coordinación del flujo de las cinco
    fases, lotes y ejecuciones. Orquesta las operaciones mediante
    contratos.
-   **Domain:** entidades, objetos de valor, enumeraciones y reglas de
    negocio propias del proceso.
-   **Infrastructure:** implementaciones técnicas para Excel, PDF,
    correo, archivos, persistencia con EF Core (SQLite y, más adelante,
    SQL Server), seguridad y logging.

Estructura orientativa:

``` text
FonPrebel/
├── Presentation/
│   ├── Views/
│   ├── ViewModels/
│   ├── Commands/
│   └── Resources/
│
├── Application/
│   ├── UseCases/
│   ├── Services/
│   ├── Interfaces/
│   └── DTOs/
│
├── Domain/
│   ├── Entities/
│   ├── Enums/
│   ├── ValueObjects/
│   └── Interfaces/
│
├── Infrastructure/
│   ├── Excel/
│   ├── Pdf/
│   ├── Email/
│   ├── Database/
│   ├── Security/
│   ├── FileStorage/
│   └── Logging/
│
├── Assets/
│   └── (logo de la empresa; ver apartado 2.20)
│
└── FonPrebel.csproj
```

La estructura es una guía inicial; puede ajustarse si existe una razón
técnica concreta, manteniendo las responsabilidades separadas y evitando
sobreingeniería.

### 2.3 POO, interfaces y contratos

-   Aplicar encapsulamiento, abstracción y polimorfismo cuando sean
    útiles.
-   Las interfaces representan contratos de comportamiento entre
    componentes. No son sinónimo de POO ni todos los contratos requieren
    una interfaz.
-   Crear interfaces cuando aporten desacoplamiento real, especialmente
    en integraciones externas y servicios reemplazables.
-   No crear una interfaz por cada clase de manera automática.
-   Las implementaciones deben respetar el comportamiento definido por
    sus contratos.
-   La lógica de negocio y los casos de uso no deben depender
    directamente de bibliotecas concretas de infraestructura.

Interfaces candidatas, sujetas a la necesidad real durante el diseño:

-   `IEmailService`
-   `IExcelReader`
-   `IExcelWriter`
-   `IPdfGenerator`
-   `ICredentialProvider`
-   `IExecutionRepository` o contratos de persistencia equivalentes.

### 2.4 Principios SOLID y simplicidad

Aplicar los cinco principios SOLID sin convertirlos en una obligación de
crear abstracciones innecesarias:

-   **S --- Single Responsibility:** cada clase debe tener una
    responsabilidad clara.
-   **O --- Open/Closed:** permitir incorporar implementaciones sin
    modificar innecesariamente la lógica estable.
-   **L --- Liskov Substitution:** las implementaciones deben respetar
    el contrato de sus abstracciones.
-   **I --- Interface Segregation:** interfaces pequeñas y enfocadas.
-   **D --- Dependency Inversion:** la lógica de alto nivel debe
    depender de abstracciones y no de detalles concretos.

No introducir Mediator, buses, CQRS, repositorios genéricos, fábricas u
otros patrones por moda. Utilizarlos únicamente si un problema concreto
los justifica.

### 2.5 Límite obligatorio de líneas por archivo

**Ningún archivo de código fuente podrá superar las 400 líneas de
código.**

Aplica a clases C#, interfaces, ViewModels, servicios, entidades,
archivos de código asociado a WPF y demás archivos de código fuente.

-   Si un archivo se aproxima al límite, revisar sus responsabilidades y
    dividirlo en componentes coherentes.
-   No dividir clases artificialmente solo para cumplir el número.
-   El límite no obliga a crear clases o archivos innecesarios.
-   Los archivos de documentación, scripts SQL, configuración y recursos
    no son archivos de código fuente sujetos a este límite, salvo que
    posteriormente se defina lo contrario.
-   El código autogenerado por herramientas (migraciones de EF Core,
    `ModelSnapshot` y archivos `.Designer.cs`) queda exceptuado del
    límite y no se edita a mano.

### 2.6 Base de datos SQLite

SQLite será una base de datos local del equipo del analista. Se
utilizará para información operativa que necesita persistencia y
consulta:

-   Configuración de la aplicación.
-   Configuración del correo (servidor, puerto, seguridad, cuenta y
    remitente) y credenciales cifradas.
-   Rutas del servidor de archivos y demás parámetros editables.
-   Identificación y estado de los lotes.
-   Historial de ejecuciones.
-   Estado de las fases.
-   Seguimiento de los jubilados incluidos en cada lote.
-   Estado de los envíos de correo y resultados necesarios para reanudar
    o consultar el proceso.

SQLite **no** se utilizará para almacenar los archivos Excel ni los PDF.
Tampoco se utilizará como destino de los logs.

La base de datos no será compartida por varios equipos ni se ubicará en
el servidor de archivos compartido: será local al equipo del analista.

Esta base local es independiente de la base central de SQL Server
prevista para el inicio de sesión (apartado 2.23). No contiene los
usuarios ni las contraseñas de acceso al aplicativo.

El acceso a SQLite se hace con Entity Framework Core (apartado 2.7).
Como los trabajadores que generan PDFs y envían correos escriben
estado en paralelo (apartado 2.16), se aplican estas reglas:

-   Cada operación usa un contexto de vida corta, creado con
    `IDbContextFactory`. Un contexto no se comparte entre hilos.
-   Se activa el modo WAL y un tiempo de espera ante bloqueo para las
    escrituras simultáneas. Los valores quedan pendientes (apartado
    5.4).

Ubicación propuesta para los datos locales:

``` text
%LOCALAPPDATA%/
└── FonPrebel/
    ├── Database/
    │   └── FonPrebel.db
    ├── Logs/
    │   └── YYYY-MM-DD/
    │       ├── execution.log
    │       └── errors.log
    └── Salida/
```

La carpeta `Salida/` contiene el Excel final de la fase 5 (ver Fase 5
en la sección 3). Se propone ubicarla en la carpeta de datos del
usuario y no junto al ejecutable, porque una carpeta de instalación
puede no permitir escritura. La ubicación definitiva y los permisos
deberán validarse con TI. No
guardar la base de datos dentro de la carpeta de instalación si esta
requiere permisos de administrador o no permite escritura al usuario.

### 2.7 Esquema, migraciones y acceso a datos

El acceso a datos se implementa con **Entity Framework Core**. No se
escribe SQL en texto dentro del código: las consultas se expresan en
LINQ y el esquema se define con entidades y configuraciones en C#.

Requisitos:

-   No hay consultas SQL quemadas en el código ni en archivos `.sql` que
    la aplicación consulte. Si aparece un caso que EF Core no pueda
    resolver, se consulta antes de recurrir a SQL.
-   El esquema se define con entidades y configuraciones (Fluent API) en
    `Infrastructure/Database`. Los índices y las vistas, si se
    requieren, se definen allí y se versionan con las migraciones.
-   Las consultas se organizan por funcionalidad en los repositorios o
    servicios de datos, y deben ser fáciles de localizar.
-   El acceso a datos se expone mediante contratos específicos, por
    ejemplo `IExecutionRepository`, sin repositorios genéricos
    (apartado 2.4).
-   Los cambios del esquema se gestionan con migraciones de EF Core,
    versionadas y guardadas en el repositorio.
-   La aplicación debe poder identificar la versión del esquema y
    aplicar las migraciones pendientes al iniciar, de manera controlada.
-   Las migraciones no deben borrar el historial existente ni recrear la
    base de datos en cada actualización.
-   La creación inicial de la base local debe ser reproducible desde las
    migraciones. La aplicación podrá inicializar el archivo local y
    aplicar el esquema automáticamente.
-   Documentar las tablas, columnas, tipos, propósito y relaciones en
    `DatabaseDictionary.md`.
-   Si TI necesita revisar el SQL, se exporta un script desde las
    migraciones. Ese script es solo documentación: no es la fuente de
    verdad ni lo ejecuta la aplicación.
-   Los nombres y columnas definitivos de las tablas se definirán al
    diseñar el modelo de datos; no se deben inventar campos de negocio
    que aún no estén definidos.

Estructura orientativa:

``` text
Infrastructure/Database/
├── AppDbContext.cs
├── Configurations/
├── Migrations/          (autogenerado por EF Core)
└── Repositories/

database/
├── exported-scripts/    (opcional, solo documentación)
└── DatabaseDictionary.md
```

Tablas candidatas identificadas hasta el momento:

  -----------------------------------------------------------------------
  Tabla propuesta                     Propósito
  ----------------------------------- -----------------------------------
  `Batch`                             Identificar cada lote de
                                      procesamiento.

  `BatchExecution`                    Registrar las ejecuciones o
                                      reanudaciones de un lote.

  `BatchRecipient`                    Registrar cada jubilado incluido en
                                      un lote y su seguimiento.

  `PhaseExecution`                    Registrar estado y resultado de
                                      cada fase.

  `EmailConfiguration`                Guardar servidor, puerto,
                                      seguridad, cuenta y remitente del
                                      correo, y el secreto cifrado con
                                      DPAPI.

  `AppConfiguration`                  Guardar configuraciones generales
                                      de la aplicación, incluidas las
                                      rutas del servidor de archivos.
  -----------------------------------------------------------------------

Esta lista es preliminar. Las relaciones, columnas, claves,
restricciones e índices deben diseñarse y documentarse antes de
implementar el esquema definitivo.

### 2.8 Gestión de lotes, ejecuciones e historial

El procesamiento debe organizarse por **lotes**. Un lote representa un
conjunto de trabajo identificado, asociado a un periodo y a los
archivos/datos utilizados para procesar a los jubilados.

Se debe distinguir entre:

-   **Lote (`Batch`):** unidad de trabajo identificable. Agrupa los
    jubilados y el proceso mensual correspondiente.
-   **Ejecución (`BatchExecution`):** cada intento de ejecutar o
    continuar el procesamiento de un lote. Un mismo lote puede tener más
    de una ejecución cuando se reanuda o se realizan reintentos
    controlados.
-   **Fase (`PhaseExecution`):** seguimiento de cada una de las cinco
    fases dentro del lote/ejecución.
-   **Destinatario (`BatchRecipient`):** seguimiento individual de cada
    jubilado dentro del lote, especialmente para conocer el resultado
    del PDF y del envío.

Reglas acordadas:

1.  Cada lote debe tener un identificador único y legible.
2.  El identificador debe permitir reconocer el lote y relacionarlo con
    su fecha/periodo.
3.  El formato definitivo del identificador está pendiente de
    confirmación. Como ejemplo ilustrativo se mencionó `2026-11-002`; no
    debe considerarse todavía un formato obligatorio.
4.  Si la aplicación se cierra, el equipo se apaga o el proceso se
    interrumpe, al volver a abrirla debe consultar el estado persistido
    y ofrecer continuar el mismo lote cuando sea seguro hacerlo.
5.  Reanudar un lote no debe crear automáticamente un lote nuevo.
6.  Los correos que ya estén confirmados como enviados no deben volver a
    enviarse durante una reanudación normal.
7.  Los destinatarios pendientes o con error deben poder identificarse y
    gestionarse mediante reintentos controlados.
8.  Si el analista decide iniciar un procesamiento nuevo con archivos o
    información corregidos, debe crearse un lote nuevo, conservando el
    anterior para trazabilidad.
9.  No se deben sobrescribir ni eliminar los registros históricos de
    lotes anteriores.
10. Debe existir una sección de historial para consultar lotes
    anteriores, sus ejecuciones, resultados y estados de notificación.
11. El diseño debe contemplar estados de envío como pendiente, enviado y
    fallido. Se deberá definir cómo representar un resultado incierto
    cuando se pierde la conexión durante el envío y no se sabe si el
    servidor recibió el correo.
12. La aplicación no debe afirmar que un correo fue enviado si no
    recibió la confirmación correspondiente del mecanismo de envío.

La persistencia del estado por destinatario permitirá conocer cuáles
correos fueron confirmados, cuáles fallaron y cuáles siguen pendientes.
No se debe asumir que una interrupción de red permite garantizar por sí
sola una entrega exactamente una vez; los estados inciertos deben
tratarse de forma explícita para evitar duplicados.

### 2.9 Configuración de la aplicación

La configuración que puede cambiar con el tiempo (servidor de correo,
puerto, rutas del servidor de archivos y similares) se administra desde
la sección **Configuración** de la interfaz y se guarda en SQLite.
**No se utiliza un archivo `.env`.**

Razones de la decisión:

-   Un solo lugar para todo: el analista cambia la configuración y la
    credencial en la misma pantalla, sin editar archivos de texto.
-   La aplicación valida los valores y puede probar la conexión al
    guardar, lo que evita que un error de tipeo en un archivo detenga el
    envío.
-   Es coherente con la gestión de credenciales (apartado 2.10.1), que
    ya vive en esa misma sección.
-   Hay un solo analista y una sola máquina; no existe un entorno de
    despliegue que justifique archivos de configuración externos.
-   Un `.env` incluido en el compilado no sería editable sin recompilar,
    y ubicado junto al ejecutable podría requerir permisos de
    administrador para modificarlo.

#### 2.9.1 Parámetros editables

La lista es una propuesta pendiente de confirmación. **Ningún parámetro
tiene un valor por defecto inventado**: los valores reales los entregan
TI y el analista. Los nombres definitivos de tablas y columnas se
definen al diseñar el modelo de datos.

| Grupo | Parámetro | Dónde se guarda | Estado |
|---|---|---|---|
| Correo | Servidor | `EmailConfiguration` | Valor por confirmar con TI |
| Correo | Puerto | `EmailConfiguration` | Obligatorio siempre; valor por confirmar con TI |
| Correo | Seguridad de la conexión | `EmailConfiguration` | Valores admitidos por definir |
| Correo | Proxy activado (interruptor) | `AppConfiguration` | Según el entorno: con o sin proxy |
| Correo | Proxy (campo de texto) | `AppConfiguration` | Obligatorio solo si el proxy está activado; formato por confirmar con TI |
| Correo | Cuenta (usuario) | `EmailConfiguration` | Referencia en el apartado 2.10.1; validar con TI |
| Correo | Remitente | `EmailConfiguration` | Por confirmar |
| Correo | Contraseña | `EmailConfiguration`, cifrada con DPAPI | Se gestiona según el apartado 2.10.1 |
| Correo | Tiempo máximo de espera por envío | `AppConfiguration` | Valor pendiente |
| Servidor de archivos | Rutas 1 a 4 | `AppConfiguration` | Pendiente; las rutas anteriores están desactualizadas |
| Conexiones | Intervalo de verificación automática | `AppConfiguration` | 2 minutos (requisito actual) |
| Conexiones | Umbral de conexión lenta (ms) | `AppConfiguration` | Valor pendiente |
| Concurrencia | Trabajadores para PDFs y para correos | `AppConfiguration` | Valores pendientes |

Lo que **no** es configuración editable:

-   Las reglas de negocio y la estructura de las cinco fases.
-   Las columnas a copiar de cada tipo de póliza y la llave del cruce.
    Son reglas de negocio; dónde se almacenan se define cuando se
    confirmen (apartado 5.7).
-   Los datos de los jubilados (cédulas, correos, valores).

#### 2.9.2 Comportamiento

1.  En la primera ejecución no hay configuración. La aplicación muestra
    una alerta con acceso a la sección Configuración y no ejecuta las
    operaciones que dependan de lo que falte (apartado 2.10.2).
2.  Al guardar, la aplicación valida los campos obligatorios y su
    formato (por ejemplo, que el puerto sea numérico) con los mismos
    indicadores visuales del apartado 2.10.1, y ofrece probar la
    conexión (apartado 2.14).
3.  Los parámetros que no son secretos se guardan en SQLite sin cifrar.
    Los secretos se guardan siempre cifrados con DPAPI.
4.  Ante un cambio de servidor de correo, el analista actualiza
    servidor, puerto y seguridad en Configuración, prueba la conexión
    y, si cambió la cuenta, actualiza la contraseña según el apartado
    2.10.1.
5.  Si falta un parámetro obligatorio o es inválido, la operación
    afectada no arranca y se muestra un mensaje claro con el nombre del
    parámetro. La aplicación no asume valores.
6.  Los logs no registran contraseñas ni secretos al guardar o validar
    la configuración.
7.  Proxy: la pantalla de Configuración incluye un interruptor. Apagado,
    el campo de texto del proxy queda deshabilitado y vacío, y la
    aplicación se conecta directamente. Encendido, el campo se
    habilita y es obligatorio; al guardar, la aplicación envía los
    correos y prueba la conexión a través de ese proxy (apartado
    2.14). La aplicación debe adaptarse a que el entorno tenga o no
    proxy sin cambiar código.
8.  El puerto del servidor de correo es obligatorio siempre, con o sin
    proxy.
9.  No se requiere usuario ni contraseña para el proxy: el analista
    indicó que no se necesita. Si en el futuro se exigiera, sería
    otro secreto cifrado con DPAPI y se agregaría en una versión
    posterior.

### 2.10 Credenciales y seguridad

Las credenciales no pueden estar quemadas en el código fuente ni
almacenarse en texto plano en archivos de configuración o en SQLite.

Decisión acordada:

-   Utilizar DPAPI de Windows para cifrar las contraseñas antes de
    persistirlas en SQLite.
-   La base de datos guardará el valor cifrado, no la contraseña
    legible.
-   La aplicación descifrará la credencial únicamente cuando la necesite
    y la mantendrá en memoria el menor tiempo razonable.
-   Se propone utilizar el ámbito `CurrentUser` de DPAPI para vincular
    la protección al usuario de Windows que configura/ejecuta la
    aplicación. Esta decisión debe validarse con TI y documentarse en la
    implementación.
-   Si cambia el usuario de Windows o el perfil deja de estar
    disponible, la credencial protegida podría no ser recuperable; la
    aplicación debe permitir volver a configurarla sin perder el
    historial operativo.
-   No incluir secretos, contraseñas ni contenido sensible en los logs.
-   La protección DPAPI de credenciales no significa que toda la base
    SQLite esté cifrada. Los permisos de acceso al archivo y la
    protección de los datos personales deben evaluarse por separado.
-   Antes de elegir el mecanismo definitivo de autenticación del correo,
    confirmar si el banco dispone de SMTP, relay autorizado,
    autenticación integrada u otro mecanismo corporativo.
-   El servidor, el puerto y la seguridad de la conexión del correo no
    son secretos: se guardan en `EmailConfiguration` y se editan desde
    la pantalla de Configuración (apartado 2.9). Solo la contraseña
    se cifra con DPAPI.
-   Las credenciales de este apartado son internas del funcionamiento
    del aplicativo (por ejemplo, la cuenta de correo). El usuario y la
    contraseña para entrar a usar el aplicativo son un caso distinto y
    viven en la base central de SQL Server (apartado 2.23).

#### 2.10.1 Interfaz de configuración de credenciales

Debe existir una sección de configuración donde el analista pueda
registrar y actualizar las credenciales necesarias.

Cuenta de correo identificada en la información suministrada por el
analista: `seguros.fonprebel@prebel.com.co`. Esta dirección se incluye
como referencia de configuración y deberá validarse con TI antes de
utilizarla en producción.

**La contraseña real no debe escribirse en esta especificación, ni en
ningún documento de proyecto, código fuente, repositorio, log o archivo
de configuración en texto plano.** La captura compartida contiene una
credencial que debe considerarse expuesta; se recomienda cambiarla
inmediatamente y registrar la nueva únicamente mediante la pantalla de
configuración, para almacenarla protegida con DPAPI en SQLite.

Comportamiento visual y funcional acordado:

-   Los campos de contraseña deben ocultar el contenido mediante
    caracteres de máscara.
-   Debe existir un control de mostrar/ocultar contraseña (icono de
    ojo).
-   Los campos obligatorios vacíos deben identificarse visualmente, por
    ejemplo, con borde rojo y un mensaje claro.
-   Al completar un campo válido, puede mostrarse un estado visual de
    validación satisfactorio, por ejemplo, borde verde.
-   Al guardar correctamente, se debe mostrar confirmación y limpiar los
    indicadores temporales de validación.
-   La contraseña se cifra con DPAPI antes de almacenarse en SQLite.
-   La aplicación no debe mostrar la contraseña guardada al volver a
    abrir la configuración; debe indicar que existe una credencial
    configurada y permitir reemplazarla.
-   Junto al campo de contraseña debe existir un botón con icono de
    lápiz para entrar en modo edición. En modo normal, el campo no
    revela ni precarga la contraseña almacenada.
-   Al editar, la aplicación debe solicitar la contraseña anterior
    (la que actualmente tiene almacenada) y la contraseña nueva. Debe
    incluir un tercer campo para confirmar la contraseña nueva.
-   La contraseña anterior ingresada debe compararse localmente con
    la credencial actualmente protegida en SQLite, después de
    descifrarla temporalmente mediante DPAPI. Si no coincide, no se
    permite reemplazarla.
-   La contraseña nueva debe validarse contra el mecanismo real de
    autenticación del servidor de correo antes de reemplazar la
    credencial almacenada. Si la validación falla, se conserva la
    credencial anterior y se informa el motivo sin revelar secretos.
-   Si la contraseña se cambió directamente en el correo antes de
    actualizarla en la aplicación, el analista deberá introducir como
    contraseña anterior el valor que la aplicación tiene guardado y
    como nueva la contraseña vigente del servidor. Si el mecanismo de
    correo no permite validar la nueva credencial en ese momento, no
    se debe afirmar que quedó validada.
-   Solo después de validar los campos y la nueva credencial, la
    aplicación reemplaza el secreto cifrado en SQLite. La operación
    debe ser atómica: ante un fallo, conservar la credencial anterior.
-   No se debe registrar el contenido de los campos de contraseña en
    logs ni mensajes de error.

#### 2.10.2 Validación antes de iniciar

-   La aplicación debe validar que estén configuradas las credenciales
    obligatorias para las operaciones que se van a ejecutar.
-   Si falta una credencial necesaria para el envío de correos, el
    inicio del proceso completo o de la fase de envío debe bloquearse
    según el flujo que se defina.
-   Se mostrará una alerta clara y visible, con acceso a la sección de
    configuración. Ejemplo: "Por favor, configure las credenciales
    necesarias para el envío de correos".
-   No bloquear fases que no requieran esa credencial si el diseño
    permite ejecutarlas de forma independiente.
-   La lista definitiva de credenciales obligatorias depende de
    confirmar el protocolo y la infraestructura del banco.

### 2.11 Seguridad de los PDFs

-   Cada PDF se protege con contraseña apenas termina de generarse.
-   La contraseña es la cédula del jubilado asociado a las pólizas.
-   Pendiente: formato exacto de la cédula como contraseña (con o sin
    puntos) y si el correo indica cuál es la contraseña.
-   La regla de usar la cédula como contraseña debe validarse con las
    políticas de seguridad y protección de datos del banco. No asumir
    que esta medida, por sí sola, constituye protección suficiente.

### 2.12 Servidor de archivos y rutas

Existe un solo servidor de archivos con varias rutas de carpetas. Todas
las rutas están pendientes de definir: las que se manejaban están
desactualizadas y se confirmarán próximamente. Las rutas se registran
desde la pantalla de Configuración (apartado 2.9) y se guardan en
SQLite.

-   Ruta 1: Excel consolidado anual, con una pestaña por mes. Se llama
    `BASE DE ASEGURADOS CONSOLIDADA TODAS LAS PÓLIZAS_2026` (lleva el año
    al final). Sus columnas y el plazo mensual están en el Anexo A.
-   Ruta 2: 13 archivos, uno por tipo de póliza (probablemente Excel).
-   Ruta 3: Excel que el analista llena manualmente (también donde la
    aplicación completa los valores en la fase 2). Se llama
    `FINAL PLANILLA REPORTE MENSUAL NÓMINA JUBILADOS FONPREBEL`. Sus
    columnas están en el Anexo B.
-   Ruta 4: destino de los PDFs generados.

El informe en Excel de la fase 5 se guarda en la carpeta local `Salida/`
(apartado 2.6), no en la ruta compartida del servidor de archivos.

### 2.13 Validaciones previas

Antes de iniciar el proceso, la aplicación valida que el servidor de
archivos sea accesible y que las rutas de la fase 1 existan. Para
arrancar la fase 2, la validación funcional original es que exista el
archivo que el analista debe llenar manualmente (ruta 3). Si falta algo,
la fase no arranca y se muestra el motivo.

Además, se validarán las configuraciones y credenciales necesarias para
la operación que el analista intente ejecutar, de acuerdo con el
apartado 2.10. También se valida que el servidor de correo y las rutas
requeridas estén configurados (apartado 2.9).

### 2.14 Conexiones

-   Sección con un botón "Comprobar conexión" para el servidor de
    archivos y otro para el servidor de correo.
-   Debajo del botón, un log pequeño muestra la ejecución de la
    validación mientras corre.
-   Al final aparece un punto de estado: verde si la conexión está bien,
    amarillo si está lenta y rojo si falló.
-   Cada 2 minutos la aplicación repite esta validación sola para ambos
    servidores, mostrando los mensajes y el punto de estado.
-   Estas pruebas corren en segundo plano y no bloquean la interfaz.
-   Pendiente: definir a partir de cuántos milisegundos una conexión se
    considera lenta.
-   La prueba de correo debe validar el mecanismo real autorizado por el
    banco; un ping por sí solo no demuestra que la autenticación ni el
    envío funcionen.
-   Las pruebas usan la configuración guardada en SQLite (apartado
    2.9) y se pueden lanzar desde la propia pantalla de Configuración.
-   Si el proxy está activado en Configuración (apartado 2.9.2), las
    pruebas de correo se hacen a través de él.

### 2.15 Concurrencia y rendimiento

-   La aplicación no puede bloquearse, congelarse ni dejar la máquina
    inutilizable, especialmente durante la generación de PDFs y el envío
    de correos.
-   Al arrancar un proceso, la aplicación queda trabajando en segundo
    plano mientras el analista sigue con sus otras tareas.
-   Se evaluará el uso de colas de proceso y tareas asíncronas. La
    interfaz debe mantenerse siempre responsiva.
-   Los correos se envían uno por uno, cada jubilado con su propio
    correo y nunca en bloque.
-   El nivel de paralelismo queda pendiente de definir según los límites
    del servidor de correo.
-   La cantidad de jubilados es variable (hoy 70; puede subir o bajar,
    por ejemplo a 80 o 50). El diseño no debe depender de un número
    fijo.
-   Las operaciones largas deben contemplar cancelación controlada
    cuando sea seguro, sin dejar estados inconsistentes.
-   Utilizar `CancellationToken` en operaciones asíncronas que lo
    permitan.
-   Cada trabajador accede a SQLite con su propio contexto de vida corta
    (apartado 2.6). No se comparte un contexto entre hilos.

### 2.16 Procesamiento progresivo por jubilado

Se acuerda implementar el modelo **A: procesamiento progresivo e
independiente por jubilado** para las fases 2, 3 y 4. El objetivo es evitar
que una fase espere a que la fase anterior termine de procesar a todos los
jubilados cuando ya existen insumos completos para avanzar con algunos.

Comportamiento:

1. La fase 2 realiza el cruce y completado de la información.
2. Cuando los datos de un jubilado estén completos y hayan superado las
   validaciones necesarias, ese jubilado queda disponible para la fase 3.
3. La fase 3 puede generar el PDF de ese jubilado sin esperar a que la fase
   2 termine con los demás.
4. Cuando el PDF esté generado correctamente y exista un correo válido,
   el jubilado queda disponible para la fase 4.
5. La fase 4 puede enviar ese correo individual sin esperar a que terminen
   los demás PDFs o cruces.
6. La fase 5 genera el informe final cuando todos los trabajos del lote
   hayan alcanzado un estado terminal y los resultados puedan consolidarse.

Reglas obligatorias:

-   El procesamiento progresivo se realiza por jubilado, no enviando
    información parcial o incompleta.
-   Cada etapa debe comprobar que dispone de todos sus insumos y que estos
    son válidos antes de iniciar.
-   Un error de un jubilado no debe detener automáticamente el trabajo de
    los demás jubilados.
-   Cada trabajo debe tener un estado persistido en SQLite para permitir
    reanudación y evitar repetir tareas ya completadas.
-   No regenerar un PDF que ya esté confirmado como válido, salvo que exista
    una acción explícita de regeneración definida por el analista.
-   No reenviar correos confirmados como enviados.
-   Los correos continúan enviándose individualmente, uno por uno, respetando
    los límites y políticas del servidor corporativo.
-   Los estados fallidos, pendientes e inciertos deben permanecer
    diferenciados.
-   Las fases 1 y 2 conservan sus validaciones y confirmaciones del analista.
    El procesamiento progresivo no elimina las dependencias globales de
    archivos ni autoriza a comenzar una fase sin los insumos requeridos.
-   La aplicación debe coordinar las tareas mediante colas de trabajo o un
    mecanismo equivalente sencillo. No se requiere introducir un bus de
    mensajes ni infraestructura distribuida.
-   La concurrencia y el número de trabajadores por tipo de tarea deben
    definirse considerando el equipo del analista, el servidor de archivos,
    la biblioteca PDF y los límites del servidor de correo.
-   La interfaz debe reflejar que las fases 2, 3 y 4 pueden estar activas
    simultáneamente. No debe mostrar una fase como finalizada mientras
    existan trabajos pendientes o en ejecución de esa fase.

**Pendiente:** definir la fórmula exacta de la barra de progreso general
cuando varias fases procesan elementos simultáneamente. La barra de progreso
de la fase activa debe mostrar el avance de los pasos o elementos de esa
fase y reiniciarse al comenzar una nueva fase seleccionada como principal.

### 2.17 Logs

Se mantienen los logs en archivos. **No se almacenarán logs en SQLite.**

-   Dos tipos de log separados: log de ejecución y log de errores.
-   Ambos visibles en la interfaz.
-   Se guardan en archivos `.log`, con una carpeta por día (la carpeta
    de hoy, la de ayer, etc.).
-   Los registros que no pudieron procesarse o enviarse (por ejemplo,
    jubilados sin correo) deben quedar registrados.
-   Los logs deben incluir información útil para diagnosticar el
    proceso: fecha/hora, nivel, lote, ejecución, fase y resultado, según
    corresponda.
-   No registrar contraseñas, secretos ni información personal
    innecesaria.
-   El historial consultable de lotes y estados operativos se guarda en
    SQLite; los logs técnicos y de ejecución permanecen en archivos
    separados.

### 2.18 Interfaz y experiencia de usuario

-   La aplicación debe ser muy gráfica e intuitiva.
-   Indicador de fases visible en todo momento, con una bolita por fase
    (Fase 1 a Fase 5). La fase activa muestra un spinner o animación que
    indica que está trabajando o a la espera.
-   Una barra de progreso por cada fase y una barra de progreso general,
    al estilo de los instaladores de Windows o Visual Studio.
-   Botón de inicio para arrancar el proceso y botón de inicio (play)
    para arrancar cada fase que requiere confirmación del analista.
-   Alertas y mensajes claros cuando una fase termina o cuando se
    necesita confirmación.
-   La interfaz debe incluir las secciones descritas en el apartado
    2.19.
-   La pantalla de ejecución debe mostrar el lote activo y su
    identificador.
-   Cuando exista un lote interrumpido que pueda reanudarse, la interfaz
    debe informar al analista y ofrecer la opción de continuar el mismo
    lote.
-   La interfaz no debe presentar como enviado un correo cuyo envío no
    esté confirmado.

### 2.19 Estructura de la interfaz

La interfaz se divide en secciones separadas para que se vea limpia y
ordenada, sin todo amontonado. Se propone una navegación lateral con
estas secciones:

-   **Ejecución:** indicador de fases, lote activo, botones de inicio,
    opción de reanudación, barras de progreso y alertas.
-   **Historial:** consulta de lotes anteriores, ejecuciones,
    resultados, estados de fases y correos enviados/fallidos/pendientes.
-   **Logs:** log de ejecución y log de errores, cada uno en su propio
    panel.
-   **Configuración:** servidor de correo, puerto y seguridad, cuenta y
    remitente, rutas del servidor de archivos, intervalos y demás
    parámetros editables (apartado 2.9), además de las credenciales
    protegidas (apartado 2.10.1).
-   **Métricas:** sección aparte, porque su contenido es distinto al de
    la operación diaria (ver apartado 2.22).
-   **Conexiones:** pruebas del servidor de archivos y del servidor de
    correo (ver apartado 2.14).
-   **Inicio de sesión:** pantalla de acceso a la aplicación. Se
    desarrolla al final (ver apartado 2.23).

Se implementan como secciones de la misma aplicación; si más adelante
crecen, pueden separarse en módulos.

### 2.20 Estilo visual

-   Aspecto profesional, intuitivo y agradable.
-   Fondo blanco, con el logo de la empresa como marca de agua grande,
    visible en cualquier sección.
-   Los componentes son ligeramente transparentes para que el logo se
    vea detrás de ellos.
-   Deseable: tema claro y oscuro, manteniendo estables el fondo, la
    marca de agua y los componentes en ambos.
-   El logo lo entrega el analista dejando el archivo en la raíz del
    proyecto. El equipo de desarrollo debe moverlo a la carpeta
    `Assets/` y registrarlo como recurso de la aplicación, para que
    viaje dentro del ejecutable.
-   No se debe inventar ni sustituir el logo. Si el archivo no está
    disponible, se solicita.
-   Pendiente: nombre y formato del archivo del logo.

### 2.21 Notificaciones

-   Al terminar cada fase, la aplicación muestra una notificación
    flotante en la esquina inferior derecha de la pantalla, al estilo de
    Windows, si es técnicamente posible.
-   La aplicación sigue trabajando en segundo plano mientras el analista
    hace otras cosas, y la notificación le avisa cuándo volver.

### 2.22 Métricas

Debe existir una sección independiente llamada **Métricas**, destinada
a consultar contadores y estadísticas del funcionamiento del aplicativo.
No debe confundirse con el modal de seguimiento de una ejecución activa:
el modal muestra el avance en tiempo real del trabajo actual, mientras
que Métricas presenta información acumulada e histórica.

Indicadores iniciales propuestos para el tablero:

-   Total de lotes creados.
-   Total de ejecuciones realizadas, incluyendo reanudaciones.
-   Cantidad de lotes completados, en curso, interrumpidos y con errores.
-   Total de jubilados procesados.
-   Total de PDFs generados correctamente y con error.
-   Total de correos confirmados como enviados, fallidos, pendientes o
    con resultado incierto.
-   Porcentaje de éxito de los procesos y de los envíos, mostrando
    siempre el numerador y denominador utilizados.

Los indicadores deben poder consultarse de forma global y, cuando
corresponda, filtrarse por periodo o lote. Los valores deben calcularse
a partir de los registros persistidos en SQLite; no se deben mantener
contadores independientes que puedan desincronizarse del historial.

La lista anterior es una propuesta inicial. Los indicadores definitivos,
filtros, gráficos y periodos de comparación quedan pendientes de
confirmación.

### 2.23 Inicio de sesión y control de acceso

**Esta funcionalidad se desarrolla al final**, cuando el resto de la
aplicación esté terminado y se tenga la información de la base central.
Todo lo no definido queda marcado como pendiente y no debe inventarse.

Requisito de seguridad: para usar la aplicación, la persona debe
iniciar sesión con un usuario. La aplicación debe dejar registro de
cada sesión.

#### 2.23.1 Base de datos central

-   Motor: SQL Server. Es la base de datos que se maneja en la empresa.
-   Alcance: está pensada para varios aplicativos de la empresa. No es
    una base local de este aplicativo.
-   Estado actual: en planeación. Todavía no existe ni se tiene acceso.
-   Contenido previsto: tabla de usuarios, contraseña de cada usuario y
    a qué aplicativo o cliente pertenece.
-   Uso en este aplicativo: solo el inicio de sesión. La aplicación le
    consulta si el usuario tiene acceso. No se crean tablas de este
    aplicativo en ella.
-   Acceso: desde el equipo del analista hacia la base central.
-   Se propone que la aplicación no descargue contraseñas y que la
    verificación la haga la base, con las contraseñas almacenadas con
    hash. Debe confirmarse con quien administre la base.

#### 2.23.2 Dos tipos de credenciales

| Credencial | Para qué sirve | Dónde vive |
|---|---|---|
| Usuario y contraseña de inicio de sesión | Entrar a usar el aplicativo. | Base central de SQL Server. |
| Contraseña de la cuenta de correo | Funcionamiento interno: enviar los correos. | SQLite local, cifrada con DPAPI (apartado 2.10). |

La SQLite local no contiene usuarios del aplicativo.

#### 2.23.3 Registro de sesiones

-   Cada inicio de sesión debe registrarse, incluyendo cuánto duró la
    sesión y cuándo se desconectó.
-   Dónde se guarda este registro (base central o local) está pendiente.
    Los logs técnicos siguen en archivos (apartado 2.17), no en SQLite.

#### 2.23.4 Diseño

-   La autenticación se define como un contrato en la capa Application,
    por ejemplo `IAuthenticationService`, con su implementación en
    Infrastructure. Así puede incorporarse al final sin modificar los
    casos de uso de las fases.
-   Se propone acceder a la base central con EF Core, con un contexto y un
    proveedor (SQL Server) independientes de los de la base local.
-   La cadena de conexión y cualquier credencial de servicio no se
    guardan en texto plano (apartado 2.10). Cómo se configuran queda
    pendiente.
-   Se propone incluir la base central entre las conexiones verificadas
    (apartado 2.14) cuando exista.
-   Si la base central no está disponible, se recomienda bloquear el
    acceso por tratarse de un tema de seguridad. La decisión está
    pendiente.
-   Los pendientes de esta sección están en el apartado 5.8.

------------------------------------------------------------------------

## 3. Flujo de fases

### Fase 1: Consolidación

1.  El analista da iniciar.
2.  La aplicación se conecta al servidor de archivos y abre el Excel
    consolidado anual (ruta 1).
3.  Crea una hoja nueva con el nombre del mes actual en mayúscula, por
    ejemplo NOVIEMBRE, con las columnas descritas en el Anexo A.
4.  Entra a la ruta 2, recorre los 13 archivos de tipos de póliza, copia
    ciertas columnas de cada uno y las pega en la hoja nueva del
    consolidado. Las columnas de destino son las del Anexo A; las columnas de origen de
    cada uno de los 13 archivos y su correspondencia con ellas están
    pendientes de definir.
5.  Al terminar, la aplicación se detiene y muestra una alerta: la fase
    1 terminó y se pide confirmar si el archivo que el analista debe
    llenar ya está completo para continuar con la fase 2.
6.  El resultado y estado de la fase se registran en SQLite asociados al
    lote y a la ejecución correspondientes.

### Fase 2: Cruce y completado

1.  El analista llena manualmente el Excel de la ruta 3 y confirma. Si
    aún no está listo, la aplicación espera.
2.  El analista pulsa el botón para iniciar la fase 2.
3.  El archivo del analista contiene, por jubilado (cédula), sus rubros.
    El analista solo llena los rubros que no son pólizas (ahorro
    navideño, pago extraordinario, conceptos de salud, pensión y
    similares). Las pólizas se reconocen por número en la columna de
    póliza, que puede traer texto o número, y sus celdas de valor quedan
    vacías.
4.  La aplicación cruza ese archivo con el consolidado de la fase 1 y
    completa los valores vacíos de las pólizas. Ejemplo: si la póliza X
    vale 20 mil, se escribe 20 mil para esa persona.
5.  El archivo completado se guarda en su misma ruta del servidor. Con
    eso termina la fase 2.
6.  El estado y resultado se registran en SQLite.

Pendiente: definir la llave exacta del cruce (cédula y número de póliza,
o solo número de póliza) y qué hacer cuando una póliza no se encuentra
en el consolidado. Ambos archivos incluyen las columnas CEDULA ASOCIADO y
#POLIZA (Anexos A y B), candidatas a llave. También confirmar que el valor
que se completa en el archivo del analista corresponde a la columna CUOTA
MENSUAL, y quién calcula la columna TOTAL MENSUAL POLIZA.

### Fase 3: Generación de PDFs

1.  Por cada cédula del archivo completado, la aplicación genera un PDF
    con los rubros asociados a ese jubilado, mostrados en dos tablas. El
    diseño detallado está pendiente.
2.  Apenas termina de generarse, cada PDF se protege con contraseña. La
    contraseña es la cédula del jubilado asociado a las pólizas.
3.  Hoy son 70 jubilados, es decir, 70 PDFs, pero la cantidad es
    variable.
4.  Los PDFs se guardan en la ruta 4 del servidor de archivos.
5.  La generación corre en segundo plano y su barra de progreso muestra
    los PDFs generados sobre el total.
6.  El resultado por jubilado y el estado de la fase se registran en
    SQLite para permitir consultar y recuperar el proceso.

Pendiente: confirmar si el PDF incluye solo pólizas o también otros
conceptos como fondo navideño o ahorro de vacaciones. Los datos de cada
jubilado disponibles están en el Anexo B.

### Fase 4: Envío por correo

1.  Cada PDF se envía en un correo individual al correo indicado en el
    archivo del analista (columna CORREO ELECTRÓNICO, Anexo B). Cada jubilado recibe solo su archivo.
2.  Si el registro no tiene correo (por ejemplo, solo tiene WhatsApp),
    no se envía, queda registrado en los logs y no cuenta como correo
    disponible para enviar.
3.  El envío corre en segundo plano, uno por uno, sin bloquear la
    interfaz.
4.  La barra de progreso de la fase muestra los correos enviados sobre
    los correos disponibles. Ejemplo: de 70 registros, 40 tienen correo;
    con 20 enviados se muestra 20 de 40 (50%).
5.  Los errores de envío también se registran en el log de errores.
6.  El estado individual de cada envío se registra en SQLite asociado al
    lote y al jubilado.
7.  Al reanudar un lote, no se vuelven a enviar los correos cuyo estado
    esté confirmado como enviado.
8.  Los casos pendientes, fallidos o inciertos deben mostrarse de forma
    diferenciada. El tratamiento de reintentos y estados inciertos debe
    definirse con el mecanismo de correo del banco.

### Fase 5: Informe de notificaciones

1.  Al terminar el envío de correos, la aplicación genera un archivo
    Excel de resumen con tablas, no en formato de log.
2.  Una tabla lista las cédulas de los jubilados que sí fueron
    notificados por correo y otra las de quienes no fueron notificados,
    por ejemplo por no tener correo o por un error de envío.
3.  El Excel se guarda en la carpeta local `Salida/` de la aplicación
    (apartado 2.6). No se deja en la ruta compartida del servidor de
    archivos.
4.  Con este informe se cierra el proceso.
5.  El informe debe corresponder al lote procesado e identificarlo para
    mantener trazabilidad.
6.  Este Excel es un informe de resultado final presentado en tablas, no
    un log. Los logs técnicos y de ejecución siguen en archivos `.log`
    (apartado 2.17).

Pendiente: definir las columnas exactas de cada tabla (por ejemplo
cédula, nombre, correo, motivo de no envío) y el nombre del archivo. Los datos de cada
jubilado disponibles están en el Anexo B.

------------------------------------------------------------------------

## 4. Especificación de negocio

Reglas de negocio conocidas hasta ahora; se irán afinando.

-   El proceso es mensual y lo opera un analista; no es un bot
    desatendido.
-   El consolidado anual lleva una hoja por mes, y cada ejecución agrega
    la del mes en curso.
-   El analista tiene del día 1 al 5 de cada mes para crear, mediante
    la aplicación, la hoja del mes actual. En ese plazo la hoja debe
    estar consolidada y cruzada, los PDFs armados y los correos
    enviados. Por ejemplo, para noviembre el plazo es del 1 al 5 de
    noviembre (Anexo A).
-   Solo las pólizas se completan con valores del consolidado. Los demás
    rubros (ahorro navideño, pagos extraordinarios, salud, pensión) los
    llena el analista y no se tienen en cuenta en el cruce.
-   Se genera un PDF por jubilado, protegido con contraseña igual a su
    cédula, sujeto a validación de seguridad del banco.
-   La notificación es solo por correo electrónico y de forma
    individual. Los jubilados con únicamente WhatsApp no se notifican.
-   Al cierre se entrega un Excel con quiénes fueron notificados y
    quiénes no, guardado en la carpeta local `Salida/`. Es un informe de
    resultado en tablas, no un log.
-   El proceso se organiza en lotes identificables.
-   Una reanudación continúa el mismo lote cuando corresponda; un
    procesamiento nuevo con información corregida genera un lote nuevo.
-   Los lotes anteriores se conservan para consulta y trazabilidad.
-   El estado de lotes, ejecuciones, fases y destinatarios se persiste
    en SQLite.
-   Los logs técnicos permanecen en archivos `.log` diarios, no en
    SQLite.
-   Las credenciales se cifran con DPAPI antes de almacenarse en SQLite.
-   La configuración (servidor de correo, puerto, seguridad y rutas del
    servidor de archivos) se edita desde la pantalla de Configuración y
    se guarda en SQLite; no se usa archivo `.env`.
-   El acceso a la aplicación requerirá inicio de sesión con un usuario
    validado contra una base central de la empresa. Se desarrollará
    al final.
-   Pendiente: resto de la especificación de negocio y las métricas.

------------------------------------------------------------------------

## 5. Pendientes y decisiones por confirmar

### 5.1 Datos y reglas de negocio

-   Rutas exactas del servidor de archivos.
-   Columnas de origen de cada uno de los 13 archivos y su
    correspondencia con las 8 columnas del consolidado (Anexo A).
-   Confirmar si la reducción de 16 a 13 insumos corresponde también a
    la cantidad de tipos de póliza, o sólo a la cantidad de archivos que
    se entregan de la ruta 2.
-   Nombres y extensión de los 13 archivos de la ruta 2, y cómo se
    identifica el que corresponde a cada tipo de póliza.
-   Confirmar que RAMO y CEDULA ASOCIADO son dos columnas distintas del
    consolidado.
-   Confirmar el nombre exacto y la extensión del archivo consolidado, y
    cómo se determina el archivo de cada año.
-   Definir qué hace la aplicación si la hoja del mes ya existe en el
    consolidado.
-   Definir el plazo mensual (Anexo A): si son días calendario o
    hábiles, y si la aplicación solo informa o también advierte o bloquea
    fuera de la ventana.
-   Confirmar el nombre exacto y la extensión del archivo del analista, si
    se renueva cada mes y cómo se identifica el del mes en curso.
-   Confirmar si RIESGO ASEGURADO (archivo del analista) y RIESGO
    ASOCIADO (consolidado) son la misma columna con nombre distinto.
-   Definir el significado y quién llena las columnas MES, TOTAL MENSUAL
    POLIZA y OBSERVACIONES del archivo del analista.
-   Confirmar que CELULAR contiene el número de WhatsApp de los
    jubilados sin correo.
-   Definir qué columnas llena el analista y cuáles completa la
    aplicación en la fase 2 (Anexo B).
-   Llave del cruce entre consolidado y archivo del analista, y manejo
    de casos sin coincidencia.
-   Contenido y diseño de los PDFs (solo pólizas o también otros rubros;
    dos tablas).
-   Formato de la cédula como contraseña del PDF (con o sin puntos).
-   Columnas exactas del informe en Excel de la fase 5 (notificados y no
    notificados).
-   Resto de la especificación de negocio.

### 5.2 Correo y seguridad

-   Confirmar credenciales de correo, proxy y protocolo (SMTP u otro).
-   Confirmar si el banco dispone de relay, autenticación integrada u
    otro mecanismo corporativo.
-   Confirmar qué credenciales son obligatorias y cuáles son los
    permisos necesarios.
-   Confirmar el ámbito DPAPI apropiado (`CurrentUser` u otro) según el
    entorno y las políticas del banco.
-   Definir límites de envío, nivel de paralelismo y política de
    reintentos.
-   Definir cómo tratar un envío cuyo resultado sea incierto por una
    interrupción de conexión.
-   Validar la política de contraseña de los PDFs y el tratamiento de
    datos personales.
-   Confirmar con TI el tipo de proxy (HTTP, SOCKS u otro) y su formato
    (dirección y puerto juntos o separados), y que la biblioteca de
    correo elegida lo soporte.

### 5.3 Lotes e historial

-   Confirmar el formato definitivo del identificador de lote.
-   Definir los estados exactos de lote, ejecución, fase y destinatario.
-   Definir las condiciones precisas para permitir reanudar una fase o
    un lote.
-   Definir cuándo un analista debe continuar un lote existente y cuándo
    crear uno nuevo.
-   Definir la retención del historial y las políticas de eliminación,
    si existen.
-   Definir la fórmula exacta del progreso general cuando las fases 2,
    3 y 4 estén trabajando simultáneamente.

### 5.4 Base de datos

-   Confirmar las columnas, tipos, claves, relaciones, restricciones e
    índices de las tablas propuestas.
-   Definir la política de respaldo y recuperación de SQLite.
-   Validar ubicación local y permisos con TI.
-   Confirmar el contenido definitivo de `DatabaseDictionary.md`.
-   Definir cómo se distribuyen las migraciones con la aplicación (se
    aplican al iniciar) y si se exportan scripts SQL para TI.
-   Confirmar la versión de .NET y de EF Core.
-   Definir el tiempo de espera ante bloqueo y la configuración del
    modo WAL de SQLite.

### 5.5 Instalación y entorno corporativo

-   Validar con TI si se permite instalar aplicaciones en el equipo del
    analista.
-   Definir si el despliegue será MSI, MSIX, portable u otra modalidad
    autorizada.
-   Confirmar arquitectura objetivo de Windows y permisos de ejecución.
-   Confirmar si el equipo dispone de las dependencias y políticas
    necesarias para acceder al servidor de archivos y al correo.
-   Validar con seguridad/TI el almacenamiento local de datos,
    protección DPAPI, permisos de SQLite y tratamiento de información de
    jubilados.

### 5.6 Interfaz, recursos y métricas

-   Nombre y formato del archivo del logo para la marca de agua (se
    entrega en la raíz del proyecto y se mueve a `Assets/`).
-   Umbral en milisegundos para considerar lenta una conexión (punto
    amarillo).
-   Confirmar los indicadores, filtros y gráficos definitivos del
    tablero de métricas.
-   Definir si las métricas incluyen el cumplimiento del plazo del 1 al
    5 de cada mes.
-   Definir la fórmula del progreso general del modal de seguimiento.
-   Detalles visuales finales de la sección de historial y
    configuración.

### 5.7 Configuración y carpeta de salida

-   Valores reales del servidor de correo, puerto, seguridad, cuenta y
    remitente (los entregan TI y el analista).
-   Rutas del servidor de archivos: las que se manejaban están
    desactualizadas y se definirán próximamente.
-   Duda funcional pendiente de aclarar con el funcional; se incorporará
    cuando se resuelva.
-   Confirmar la lista final de parámetros editables (apartado 2.9.1).
-   Confirmar la ubicación de la carpeta `Salida/`: se propone la
    carpeta de datos del usuario; la alternativa, junto al ejecutable,
    depende de los permisos de escritura.
-   Nombre y estructura del Excel de salida.
-   Definir si un cambio de configuración aplica de inmediato o desde la
    siguiente operación o lote.
-   Definir si la pantalla de Configuración requiere algún control de
    acceso.
-   Definir dónde se almacenan las columnas por tipo de póliza y la
    llave del cruce.
-   Confirmar si el proxy aplica solo a las conexiones de correo o
    también a otras.

### 5.8 Inicio de sesión y base central

-   Fecha en que estará disponible la base central de SQL Server.
-   Cómo se conecta la aplicación a la base desde el equipo del analista
    (red, cadena de conexión y credenciales de servicio).
-   Si la aplicación solo lee, con un usuario de solo consulta, o también
    escribe el registro de sesiones.
-   Dónde se guarda el registro de sesiones: base central o local.
-   Qué se guarda de cada sesión (inicio, cierre, duración y
    desconexión).
-   Si habrá roles o solo acceso sí o no.
-   Esquema de la base: tablas, columnas y cómo se relaciona cada
    usuario con el aplicativo o cliente.
-   Cómo se almacenan y verifican las contraseñas (hash) y quién
    administra la base.
-   Qué ocurre si la base central no está disponible (se recomienda
    bloquear el acceso).
-   Si habrá cierre de sesión por inactividad.

------------------------------------------------------------------------

## 6. Reglas para el equipo de desarrollo y agentes de codificación

Estas reglas aplican al implementar esta especificación:

1.  Leer la especificación completa antes de modificar o crear código.
2.  No inventar rutas, columnas, llaves de cruce, reglas de negocio,
    credenciales, nombres de hojas ni formatos que estén marcados como
    pendientes.
3.  Si una decisión pendiente bloquea una implementación, solicitar
    aclaración antes de asumirla.
4.  Mantener la arquitectura sencilla descrita en este documento. No
    introducir sobreingeniería ni patrones sin una necesidad
    demostrable.
5.  Aplicar POO, interfaces y SOLID de forma pragmática.
6.  Respetar el máximo de 400 líneas de código por archivo.
7.  Mantener la interfaz desacoplada de la lógica de negocio y de
    infraestructura.
8.  No almacenar contraseñas ni secretos en texto plano, en código
    fuente, logs o archivos de configuración.
9.  Cifrar las credenciales con DPAPI antes de persistirlas en SQLite.
10. No almacenar logs en SQLite: los logs permanecen en archivos diarios
    `.log`.
11. No borrar el historial de lotes al crear un lote nuevo ni al
    actualizar la aplicación.
12. Gestionar los cambios de esquema mediante migraciones versionadas.
13. Mantener el diccionario de datos y las migraciones organizados y
    documentados.
14. No marcar un correo como enviado sin confirmación del mecanismo de
    envío.
15. Antes de implementar decisiones relacionadas con seguridad,
    instalación o acceso a recursos corporativos, respetar las políticas
    del banco y las autorizaciones de TI.
16. No usar archivos `.env` ni otros archivos de configuración en texto
    plano para contraseñas o secretos. La configuración editable vive
    en SQLite y se gestiona desde la pantalla de Configuración.
17. No inventar valores por defecto para parámetros pendientes (rutas,
    servidor de correo, puerto, seguridad, umbrales). Si falta un
    parámetro obligatorio, bloquear la operación afectada y mostrar un
    mensaje claro con el nombre del parámetro.
18. No escribir SQL en texto dentro del código: usar EF Core y LINQ.
    Si un caso no se puede resolver así, consultar antes.
19. No compartir un contexto de EF Core entre hilos. Crear contextos
    de vida corta con `IDbContextFactory`.
20. Mover el logo entregado en la raíz del proyecto a `Assets/` y
    registrarlo como recurso. No inventar ni sustituir el logo; si no
    está, solicitarlo.
21. No implementar el inicio de sesión hasta que se indique y exista
    la información de la base central. Diseñar la autenticación como
    un contrato que pueda incorporarse después (apartado 2.23). No
    inventar tablas, columnas ni roles.

------------------------------------------------------------------------

## 7. Historial de versiones

| Versión | Descripción | Estado |
|---|---|---|
| 0.2 | Especificación funcional inicial: flujo de cinco fases, Excel, PDF, correo, logs e interfaz. | Borrador anterior |
| 0.3 | Incorporación de MVVM, arquitectura por capas sencilla, POO, SOLID pragmático, límite de 400 líneas, SQLite, DPAPI, lotes, reanudación, historial y organización de scripts SQL. | Borrador anterior |
| 0.4 | Seguimiento modal con dos barras, métricas, procesamiento progresivo por jubilado y edición segura de credenciales. | Borrador anterior |
| 0.5 | Propuesta de configuración externa con archivo `.env`. Reemplazada en la versión 0.6. | Borrador anterior |
| 0.6 | Se descarta el `.env`: la configuración (correo, puerto, seguridad, rutas) se edita desde la pantalla de Configuración y se guarda en SQLite, con la contraseña cifrada con DPAPI. Se especifica la carpeta local `Salida/` para el Excel final. Se reordena y renumera la sección 2 (el procesamiento progresivo pasa a 2.16 y el flujo de fases queda continuo). No se elimina contenido de la versión 0.4. | Borrador anterior |
| 0.7 | Acceso a datos con EF Core, sin SQL en texto en el código (apartado 2.7). Proxy configurable con interruptor. Logo entregado en la raíz del proyecto y movido a `Assets/`. Inicio de sesión contra una base central de SQL Server como etapa final (apartado 2.23). Excepción del límite de 400 líneas para código autogenerado. | Borrador anterior |
| 0.8 | Anexo A: nombre y columnas del archivo consolidado y plazo mensual del 1 al 5. Referencias en la ruta 1, la fase 1, la fase 2, negocio y pendientes. | Borrador anterior |
| 0.9 | Anexo B: nombre y 12 columnas del archivo del analista que se cruza con el consolidado, y comparación entre ambos archivos. Referencias en la ruta 3, las fases 2 a 5 y los pendientes. | Borrador anterior |
| 0.10 | Aclaración del cálculo y presentación de TOTAL MENSUAL POLIZA: agrupación por asociado y póliza, suma única y combinación vertical de celdas en Excel. | Borrador anterior |
| 0.11 | La ruta 2 pasa de 16 a 13 archivos de póliza por decisión del analista. Se actualizan el resumen (apartado 1), la ruta 2 (apartado 2.12), el flujo de la fase 1 (apartado 4), los pendientes de datos y reglas de negocio (apartado 5.1) y el Anexo A. Los nombres, el contenido y la correspondencia de columnas de esos archivos siguen pendientes. | Borrador actualizado |

**La versión 0.9 no representa todavía una especificación cerrada ni
autorización para comenzar todas las funcionalidades.** Los pendientes
de la sección 5 deben resolverse progresivamente antes de implementar
las partes que dependan de ellos.

------------------------------------------------------------------------

## Anexo A. Archivo consolidado y plazo mensual

### A.1 Archivo consolidado anual

-   Ubicación: ruta 1 del servidor de archivos (apartado 2.12).
-   Nombre: `BASE DE ASEGURADOS CONSOLIDADA TODAS LAS PÓLIZAS_2026`. Es un
    archivo Excel que lleva el año al final del nombre.
-   El nombre se escribió "ASEGURSDOS" en el mensaje original; se
    interpretó como ASEGURADOS. Pendiente confirmar el nombre exacto y la
    extensión.
-   Contiene una hoja por mes. La aplicación crea la hoja del mes actual
    con el nombre del mes en mayúscula, por ejemplo NOVIEMBRE (fase 1).

### A.2 Columnas de la hoja del mes

Cada hoja mensual del consolidado tiene estas columnas, en este orden:

| N.º | Columna |
|---|---|
| 1 | RAMO |
| 2 | CEDULA ASOCIADO |
| 3 | NOMBRE ASOCIADO |
| 4 | RIESGO ASOCIADO |
| 5 | COMPAÑIA |
| 6 | CONCEPTO |
| 7 | #POLIZA |
| 8 | CUOTA MENSUAL |

Los nombres se registran tal como los entregó el analista. En el mensaje
original, RAMO y CEDULA ASOCIADO venían sin separador; se interpretaron
como dos columnas distintas, lo cual está pendiente de confirmar.

Las columnas de origen de cada uno de los 13 archivos de pólizas y cómo
se corresponden con estas 8 están pendientes de definir (apartado 5.1).

### A.3 Plazo mensual

-   El analista tiene del día 1 al 5 de cada mes para crear, mediante la
    aplicación, la hoja del mes actual.
-   En ese plazo, la hoja del mes debe estar consolidada y cruzada, los
    PDFs armados y los correos enviados.
-   Ejemplo: para noviembre, el plazo es del 1 al 5 de noviembre.
-   Pendiente: definir si son días calendario o hábiles, y si la
    aplicación solo informa el plazo o también advierte o bloquea fuera
    de la ventana. No se debe asumir ninguna de las dos hasta que se
    defina (apartado 5.1).

------------------------------------------------------------------------

## Anexo B. Archivo del analista

### B.1 Archivo

-   Ubicación: ruta 3 del servidor de archivos (apartado 2.12).
-   Nombre: `FINAL PLANILLA REPORTE MENSUAL NÓMINA JUBILADOS FONPREBEL`.
-   Es el archivo que el analista llena manualmente y que se cruza con
    el consolidado en la fase 2 (apartado 3).
-   Pendiente: extensión, si se renueva cada mes y cómo se identifica el
    del mes en curso.

### B.2 Columnas

El archivo tiene 12 columnas, en este orden. La última columna indica
si la columna también existe en el consolidado (Anexo A).

| N.º | Columna | En el consolidado |
|---|---|---|
| 1 | MES | No |
| 2 | CEDULA ASOCIADO | Sí, con el mismo nombre |
| 3 | NOMBRE ASOCIADO | Sí, con el mismo nombre |
| 4 | RIESGO ASEGURADO | Parece equivaler a RIESGO ASOCIADO; por confirmar |
| 5 | COMPAÑIA | Sí, con el mismo nombre |
| 6 | CONCEPTO | Sí, con el mismo nombre |
| 7 | #POLIZA | Sí, con el mismo nombre |
| 8 | CUOTA MENSUAL | Sí, con el mismo nombre |
| 9 | TOTAL MENSUAL POLIZA | No |
| 10 | CELULAR | No |
| 11 | CORREO ELECTRÓNICO | No |
| 12 | OBSERVACIONES | No |

Los nombres se registran tal como los entregó el analista.

### B.3 Comparación con el consolidado

-   Columnas con el mismo nombre en ambos archivos: CEDULA ASOCIADO,
    NOMBRE ASOCIADO, COMPAÑIA, CONCEPTO, #POLIZA y CUOTA MENSUAL.
-   El archivo del analista dice RIESGO ASEGURADO y el consolidado dice
    RIESGO ASOCIADO. Se mantienen como se recibieron hasta confirmar si
    es la misma columna.
-   Solo en el consolidado: RAMO.
-   Solo en el archivo del analista: MES, TOTAL MENSUAL POLIZA, CELULAR,
    CORREO ELECTRÓNICO y OBSERVACIONES.

### B.4 Uso de las columnas en el flujo

-   CORREO ELECTRÓNICO es la columna de la que se toma el destino de los
    correos en la fase 4. Los jubilados sin correo no se notifican y
    quedan registrados en los logs.
-   CEDULA ASOCIADO identifica a cada jubilado para generar su PDF en la
    fase 3 y su contraseña (apartado 2.11).
-   CEDULA ASOCIADO y #POLIZA aparecen en ambos archivos y son candidatas
    a llave del cruce. La llave definitiva está pendiente (apartado 5.1).

### B.5 Pendientes

-   Significado y responsable de las columnas MES, TOTAL MENSUAL POLIZA y
    OBSERVACIONES.
-   Si CELULAR contiene el número de WhatsApp de los jubilados sin correo.
-   Qué columnas llena el analista y cuáles completa la aplicación en la
    fase 2.
-   Si RIESGO ASEGURADO y RIESGO ASOCIADO son la misma columna.

Estos puntos están registrados también en el apartado 5.1.


------------------------------------------------------------------------

## Anexo C. Aclaración de TOTAL MENSUAL POLIZA (versión 0.10)

Esta aclaración complementa el Anexo B y prevalece sobre cualquier
interpretación anterior que indique que el total debe repetirse en cada
registro. No modifica los demás requisitos del documento.

### C.1 Criterio de agrupación

La aplicación debe agrupar los registros del archivo del analista por la
combinación de estos dos campos:

-   CEDULA ASOCIADO: identifica al jubilado.
-   #POLIZA: identifica la póliza de ese jubilado.

Por tanto, el agrupamiento no se realiza únicamente por número de póliza:
la misma póliza asociada a jubilados diferentes corresponde a grupos
diferentes.

### C.2 Cálculo

Para cada grupo (CEDULA ASOCIADO + #POLIZA), la aplicación suma una sola
vez los valores de CUOTA MENSUAL de todos los registros que pertenecen
a ese grupo. Ese resultado es el TOTAL MENSUAL POLIZA del grupo.

### C.3 Presentación en Excel: combinación de celdas

Cuando un grupo contiene varios registros, la columna TOTAL MENSUAL
POLIZA debe mostrarse como una única celda combinada verticalmente,
que abarque exactamente las filas de ese grupo. Dentro de esa celda se
muestra una sola vez el resultado de la suma.

**No se debe:**

-   Repetir el total en cada fila del grupo.
-   Crear una columna adicional por cada registro o por cada concepto.
-   Sumar registros de asociados diferentes aunque tengan el mismo
    número de póliza.

Si el grupo contiene un solo registro, no se requiere combinar varias
celdas: la celda de TOTAL MENSUAL POLIZA muestra el mismo valor de
CUOTA MENSUAL.

Para que la combinación vertical represente correctamente cada grupo,
los registros pertenecientes al mismo asociado y número de póliza deben
quedar en filas consecutivas en la hoja generada. Se conserva cada
registro y su CUOTA MENSUAL individual; únicamente se presenta el total
del grupo una vez en la celda combinada.

### C.4 Ejemplo

Si un jubilado tiene tres registros de la póliza 23147, con cuotas de
$795.060, $795.060 y $1.114.470, la aplicación calcula $2.704.590 y
combina verticalmente las tres celdas correspondientes de TOTAL MENSUAL
POLIZA. El valor $2.704.590 aparece una sola vez en esa celda combinada,
no tres veces.

| #POLIZA | CUOTA MENSUAL | TOTAL MENSUAL POLIZA |
|---|---:|---:|
| 23147 | $795.060 | (celda combinada: $2.704.590) |
| 23147 | $795.060 | (misma celda combinada) |
| 23147 | $1.114.470 | (misma celda combinada) |

La representación de la tabla es ilustrativa: en el archivo Excel debe
existir una única celda combinada que abarque las tres filas de la última
columna.


------------------------------------------------------------------------

## Anexo D. Bloqueo del botón INICIAR hasta completar la configuración (versión 0.10)

Esta regla precisa el comportamiento visual y funcional solicitado para
el inicio del proceso. Complementa los apartados 2.9, 2.10, 2.13 y 2.14.
En caso de interpretaciones diferentes sobre cuándo habilitar el inicio,
prevalece esta regla para el botón principal INICIAR.

### D.1 Estado inicial del botón

-   El botón principal **INICIAR** (con icono de reproducción / play)
    debe mostrarse deshabilitado mientras exista al menos un campo
    obligatorio de configuración sin completar o con un valor inválido.
-   El usuario no debe poder ejecutar el flujo principal mediante ese
    botón mientras la configuración no esté completa.
-   El botón no debe aparentar estar disponible ni responder a clics
    cuando se encuentre deshabilitado.

### D.2 Campos obligatorios

-   Todos los campos definidos como parte de la configuración de la
    aplicación son obligatorios para habilitar el inicio; no se permite
    dejar campos de configuración requeridos vacíos.
-   Esto incluye, como mínimo, los datos de conexión y envío de correo
    (servidor, puerto, cuenta/correo y contraseña), los parámetros de
    seguridad que correspondan y las rutas requeridas del servidor de
    archivos, incluidas las cuatro rutas descritas en el apartado 2.12.
-   También son obligatorios los demás campos que aparezcan en la
    pantalla de Configuración como requeridos. No se deben inventar
    valores por defecto para completar campos pendientes.
-   La contraseña debe considerarse configurada cuando exista una
    credencial válida guardada de forma segura; nunca debe mostrarse
    como texto plano para indicar que está completa.

### D.3 Habilitación del botón

El botón **INICIAR** solo se habilita cuando se cumplen todas estas
condiciones:

1. Todos los campos obligatorios de Configuración tienen un valor.
2. Los valores superan las validaciones de formato y consistencia que
   correspondan (por ejemplo, puerto válido y rutas con formato
   aceptable).
3. Las credenciales y rutas requeridas para ejecutar el flujo están
   configuradas.

Si falta un dato, el botón permanece deshabilitado. Si posteriormente
se elimina o invalida un dato obligatorio, el botón vuelve a quedar
deshabilitado automáticamente.

La habilitación por completar los campos no reemplaza las validaciones
operativas de conexión y acceso descritas en los apartados 2.13 y 2.14.
Si una conexión o recurso requerido no está disponible al iniciar, la
aplicación debe impedir la ejecución y explicar el motivo.

### D.4 Mensaje de advertencia y presentación visual

-   Mientras el botón esté deshabilitado por configuración incompleta,
    se debe mostrar un mensaje visible junto al botón o en una zona
    claramente asociada a él.
-   Texto sugerido: **"Completa todos los campos obligatorios de
    Configuración para habilitar el inicio del proceso."**
-   La advertencia debe usar un tono amarillo o naranja suave, con
    suficiente contraste para llamar la atención sin resultar chillón.
-   La apariencia debe respetar la línea visual que ya se está definiendo
    para la aplicación.
-   El mensaje debe actualizarse cuando cambie el estado de la
    configuración. Cuando todo esté completo y válido, la advertencia
    de configuración incompleta deja de mostrarse y el botón puede
    habilitarse.

### D.5 Orientación al usuario

La interfaz debe permitir identificar qué campos faltan. Cada campo
obligatorio incompleto o inválido debe mostrar su estado y, cuando sea
posible, un mensaje concreto. El usuario debe poder ir directamente a
Configuración para completar los datos.

Esta regla establece un bloqueo general del botón principal **INICIAR**:
no se debe habilitar el proceso principal con una configuración parcial.


------------------------------------------------------------------------

## Anexo E. Validación real de configuración, monitoreo de recursos, logs y alertas críticas (versión 0.10)

Este anexo complementa los anexos D y las reglas existentes de
Configuración, ejecución y manejo de errores. No reemplaza ni elimina
ningún requisito anterior.

### E.1 Validación real después de introducir cada dato

Completar un campo no significa que su contenido sea correcto ni que el
recurso exista. La aplicación debe comprobar que los datos configurados
son utilizables.

-   Al introducir o modificar una ruta, correo, servidor, puerto,
    credencial u otro dato que requiera comprobación, la aplicación debe
    ejecutar la validación correspondiente.
-   Mientras la validación esté en curso, debe mostrarse un **spinner de
    carga junto al input correspondiente**, para indicar visualmente que
    el dato está siendo comprobado.
-   El spinner debe desaparecer al terminar la comprobación y dar paso a
    un estado visual inequívoco: validación exitosa o validación fallida.
-   No se debe mostrar un campo como validado únicamente porque contiene
    texto.
-   Si el usuario modifica un valor que ya había sido validado, su estado
    de validación anterior debe invalidarse y debe realizarse una nueva
    comprobación.
-   Si la validación falla, el campo debe quedar identificado como
    incorrecto y debe mostrarse un mensaje concreto que explique el
    motivo, siempre que pueda determinarse.

### E.2 Comprobación de correo y servicio de envío

La configuración del correo debe validarse mediante comprobaciones
reales, no solo mediante la presencia de valores:

-   Validar el formato de la dirección de correo.
-   Comprobar que el servidor y el puerto configurados sean alcanzables.
-   Comprobar la conexión y el mecanismo de seguridad configurado
    (por ejemplo, TLS/SSL cuando corresponda).
-   Comprobar la autenticación con las credenciales suministradas.
-   Cuando sea técnicamente posible y esté habilitado para la cuenta,
    realizar una prueba controlada de envío o una verificación equivalente
    que confirme que el servicio puede utilizarse.
-   Mostrar el spinner junto al campo o conjunto de campos que se esté
    validando y comunicar el resultado de la comprobación.

No se debe considerar válido el correo solo porque la dirección tenga
un formato correcto o porque el usuario haya escrito una contraseña.

### E.3 Comprobación de las rutas y los archivos requeridos

Cada una de las rutas configuradas debe verificarse individualmente:

1. Comprobar que la ruta tenga un formato válido.
2. Comprobar que la ubicación exista y sea accesible desde el contexto
   en el que se ejecuta la aplicación.
3. Comprobar los permisos necesarios de lectura y/o escritura según el
   uso de esa ruta dentro del proceso.
4. Cuando la ruta corresponda a un archivo requerido, comprobar que el
   archivo exista, que sea el archivo esperado y que pueda abrirse y
   leerse.
5. Cuando corresponda a una carpeta de entrada o salida, comprobar que
   se pueda acceder y que existan los permisos necesarios para la
   operación prevista.

La validación debe comprobar el recurso real. No basta con verificar que
la cadena de texto de la ruta no esté vacía.

Cada ruta debe presentar su propio estado de validación junto al input.
Si una ruta falla, se debe indicar cuál es y cuál fue la inconsistencia
detectada; por ejemplo: ruta inexistente, archivo no encontrado, acceso
denegado, recurso no disponible o permiso insuficiente.

### E.4 Condición definitiva para habilitar INICIAR

El botón principal **▶ INICIAR** solo podrá habilitarse cuando se
cumplan simultáneamente todas estas condiciones:

1. Todos los campos obligatorios están completos.
2. Todos los campos que requieren validación han sido comprobados.
3. Todas las validaciones terminaron satisfactoriamente.
4. El correo y su servicio de envío superaron las comprobaciones
   aplicables.
5. Las cuatro rutas requeridas y los archivos o carpetas asociados
   existen y tienen los permisos necesarios.
6. No hay validaciones pendientes ni errores de configuración.

Mientras haya un spinner activo, una validación pendiente o un error,
el botón debe permanecer deshabilitado.

Si un dato cambia después de haber sido validado, se debe invalidar su
resultado anterior y deshabilitar INICIAR hasta completar nuevamente
la validación de ese dato y confirmar que toda la configuración sigue
siendo válida.

La aplicación no debe permitir iniciar el proceso basándose únicamente
en que todos los inputs contienen algún valor.

### E.5 Monitoreo continuo durante la ejecución

La validación inicial no es suficiente. Los recursos pueden cambiar o
dejar de estar disponibles después de habilitar el botón y comenzar el
proceso.

Durante toda la ejecución, la aplicación debe comprobar que los
archivos, rutas, carpetas, permisos y servicios externos requeridos
continúen disponibles cuando se necesiten.

Como mínimo, debe detectar estas situaciones:

-   Un archivo configurado fue eliminado.
-   Un archivo cambió de nombre o fue movido.
-   Una ruta dejó de existir.
-   Una carpeta o recurso de red dejó de estar disponible.
-   La aplicación perdió permisos de lectura o escritura.
-   El archivo existe, pero ya no puede abrirse o leerse.
-   El servidor de correo dejó de estar disponible o rechazó la
    autenticación o el envío.
-   Cualquier otro recurso indispensable para continuar el flujo dejó
    de cumplir las condiciones verificadas al inicio.

La aplicación debe volver a comprobar el acceso al recurso en el
momento de utilizarlo. No debe confiar exclusivamente en el resultado
de la validación inicial ni en información almacenada en memoria.

### E.6 Detención segura ante una inconsistencia crítica

Si durante la ejecución se detecta que una ruta, archivo, permiso o
servicio indispensable dejó de estar disponible o ya no cumple las
condiciones requeridas:

1. Detener el proceso de forma controlada.
2. No continuar con los siguientes pasos del flujo mientras la
   inconsistencia no se resuelva.
3. Evitar continuar generando, modificando o enviando información con
   datos incompletos o recursos incorrectos.
4. Registrar el incidente en los logs con la fecha y hora, el recurso
   afectado, la operación que se intentaba realizar, el error detectado
   y el estado del proceso.
5. Mostrar inmediatamente una alerta modal clara y visible al usuario.

La detención debe preservar, en la medida de lo posible, los archivos
ya procesados y el estado necesario para identificar dónde se detuvo
la ejecución. No se debe presentar el proceso como exitoso si fue
interrumpido por una inconsistencia.

### E.7 Modal de alerta crítica

Ante una inconsistencia que impida continuar, se debe mostrar un modal
de alerta crítica, visible y prioritario, con una identidad visual de
advertencia de alto impacto, inspirada en una alerta de seguridad o
emergencia.

La presentación debe utilizar:

-   Rojo para identificar el error crítico y la detención del proceso.
-   Amarillo o naranja para advertencias y datos que requieren atención.
-   Contraste suficiente y una jerarquía visual clara, sin sacrificar
    legibilidad ni convertir la interfaz en una composición estridente.
-   Un icono de alerta claramente reconocible y un título que indique
    que el proceso fue detenido.

El modal debe informar, como mínimo:

-   **Qué ocurrió:** descripción concreta de la inconsistencia.
-   **Dónde ocurrió:** ruta, archivo, carpeta, servidor o recurso
    afectado.
-   **Qué estaba haciendo la aplicación:** operación o etapa del flujo.
-   **Por qué se detuvo:** motivo por el que no es seguro continuar.
-   **Qué debe hacer el usuario:** acción concreta para corregir el
    problema, por ejemplo restaurar el archivo, corregir la ruta o
    recuperar los permisos.
-   **Estado del proceso:** indicar que la ejecución quedó detenida y
    que no continuará hasta resolver la inconsistencia.

Ejemplo de mensaje:

**PROCESO DETENIDO — ARCHIVO NO DISPONIBLE**

La aplicación detectó que el archivo configurado en la ruta
`[ruta del archivo]` ya no existe o no está disponible.

- Recurso afectado: [nombre del archivo].
- Etapa: [etapa del proceso].
- Motivo: [detalle del error detectado].

El proceso se detuvo para evitar continuar con información incompleta.
Verifica que el archivo exista, que conserve su nombre y que la
aplicación tenga permisos de acceso. Una vez corregida la inconsistencia,
vuelve a validar la configuración antes de iniciar nuevamente.

Los datos entre corchetes son valores dinámicos que la aplicación debe
reemplazar por la información real del incidente; no deben mostrarse
literalmente al usuario.

El modal debe ofrecer una acción para cerrar o reconocer la alerta y,
cuando corresponda, una acción para abrir la configuración. Cerrar el
modal no debe reanudar automáticamente el proceso ni borrar el error.

### E.8 Logs y trazabilidad de validaciones e incidentes

La aplicación debe registrar los eventos relevantes de validación y
ejecución para facilitar el diagnóstico:

-   Inicio y finalización de cada validación.
-   Resultado de la validación: exitosa o fallida.
-   Tipo de recurso validado y referencia a la configuración afectada.
-   Pérdida de acceso, archivo eliminado, cambio de nombre, ruta
    inexistente o permisos insuficientes.
-   Fallos de conexión, autenticación o envío del correo.
-   Momento y etapa en que se detuvo el proceso.
-   Acción correctiva o revalidación posterior, cuando ocurra.

Cada registro debe incluir fecha y hora, nivel del evento, etapa,
operación, resultado y detalle técnico suficiente para diagnosticarlo.
Los niveles deben distinguir al menos información, advertencia y error
crítico.

Los logs no deben almacenar contraseñas, tokens, secretos ni
credenciales en texto plano. Los mensajes visibles al usuario deben
ser claros y accionables; el detalle técnico debe quedar en los logs
para diagnóstico.

### E.9 Criterio de aceptación

La funcionalidad se considera implementada correctamente solo si se
verifican, como mínimo, estos escenarios:

1. Se escribe una ruta inexistente: aparece el spinner, la validación
   falla, se informa el motivo e INICIAR permanece deshabilitado.
2. Se escribe una ruta válida y accesible: aparece el spinner, la
   validación termina satisfactoriamente y se refleja el estado válido.
3. Se completan los campos, pero el servidor de correo o sus
   credenciales son incorrectos: INICIAR permanece deshabilitado.
4. Todos los campos y recursos superan sus validaciones: INICIAR se
   habilita.
5. Después de validar una ruta, se elimina o renombra el archivo:
   durante la ejecución se detecta la inconsistencia, se detiene el
   proceso, se registra el evento y se muestra el modal crítico.
6. Se revocan los permisos de acceso durante la ejecución: la aplicación
   detecta el fallo al intentar utilizar el recurso, detiene el proceso,
   registra el incidente y explica el problema en el modal.
7. Se corrige una ruta o credencial previamente inválida: se ejecuta
   una nueva validación; no se reutiliza el resultado fallido ni se
   habilita INICIAR antes de confirmar el nuevo resultado.

------------------------------------------------------------------------


# ANEXO F. RECUPERACIÓN Y REANUDACIÓN SEGURA DE EJECUCIONES

## F.1. Objetivo

Definir el comportamiento de la aplicación cuando una ejecución se interrumpe por la pérdida, movimiento, eliminación, modificación o inaccesibilidad de un insumo, así como ante un cierre inesperado o apagado del equipo. La recuperación debe evitar reprocesamientos innecesarios, duplicación de resultados y uso de insumos distintos de los que fueron validados originalmente.

Este anexo complementa las reglas de validación, alertas, monitoreo y detención establecidas en los anexos anteriores. No reemplaza ni modifica su contenido.

## F.2. Decisiones funcionales confirmadas

Se establecen como decisiones definitivas las siguientes:

1. **Reanudación:** cuando se resuelva una inconsistencia, la aplicación deberá reanudar la ejecución desde el último punto confirmado, previa validación satisfactoria de los recursos requeridos.
2. **Insumos modificados:** si un insumo recuperado presenta modificaciones respecto del archivo utilizado y validado originalmente, la aplicación deberá bloquear la reanudación automática y solicitar revisión.
3. **Apagado inesperado:** al iniciar nuevamente la aplicación después de un cierre inesperado o apagado del equipo, deberá recuperar el estado persistido de la ejecución y validar los recursos antes de permitir continuar.
4. **Resultados parciales:** se conservarán los resultados parciales que estén completos, confirmados y sean válidos. La reanudación no deberá duplicarlos. Los elementos incompletos o inconsistentes deberán descartarse o regenerarse de forma controlada.

## F.3. Persistencia del estado y puntos de control

La aplicación deberá guardar el progreso de forma persistente para que una interrupción no obligue a comenzar desde cero.

- Se deberá registrar el estado general de la ejecución y el avance por cada unidad de trabajo procesable.
- Se deberá guardar un punto de control (*checkpoint*) después de completar y confirmar cada unidad de trabajo.
- El punto de control deberá permitir identificar qué archivos, jubilados, pólizas, pestañas o documentos ya fueron procesados, según corresponda al flujo real de ejecución.
- El estado persistido deberá distinguir entre trabajo pendiente, en ejecución, completado, fallido y pendiente de revisión.
- La confirmación de un resultado y la actualización de su punto de control deberán realizarse de manera consistente, para evitar que un resultado válido quede registrado como pendiente o que un trabajo incompleto figure como terminado.
- La información persistida deberá permitir identificar la ejecución y los insumos asociados a ella.

## F.4. Validación de identidad e integridad de los insumos

La aplicación no deberá considerar que un archivo recuperado es el mismo insumo únicamente porque conserva el mismo nombre o ubicación.

Para cada insumo utilizado en la ejecución se deberá conservar una huella de identificación e integridad (*fingerprint*), calculada a partir del contenido del archivo mediante un mecanismo consistente, por ejemplo, un hash criptográfico.

Al detectar nuevamente un insumo después de una interrupción:

- Se comprobará su existencia y accesibilidad.
- Se comprobarán los permisos y las condiciones necesarias para leerlo o utilizarlo.
- Se calculará nuevamente su huella y se comparará con la registrada al iniciar o validar la ejecución.
- Si la huella coincide, y las demás validaciones son satisfactorias, el insumo podrá utilizarse para reanudar.
- Si la huella no coincide, el insumo se considerará modificado o sustituido. La aplicación bloqueará la reanudación automática y solicitará revisión.
- Si el insumo no está disponible, la ejecución permanecerá detenida hasta que se restablezca el recurso o se cancele la ejecución.

La revisión de un insumo modificado deberá permitir determinar explícitamente cómo proceder. No se deberá aceptar silenciosamente un archivo distinto ni mezclar resultados de una ejecución anterior con datos nuevos.

## F.5. Comportamiento ante pérdida o inaccesibilidad de un recurso durante la ejecución

Cuando un insumo o recurso necesario desaparezca, sea movido, eliminado o deje de estar accesible durante el procesamiento:

1. La aplicación detendrá de forma controlada las operaciones que dependan del recurso afectado.
2. No iniciará nuevas unidades de trabajo que dependan de ese recurso.
3. Guardará el estado y el último punto de control confirmado.
4. Registrará el incidente en el log, incluyendo fecha y hora, recurso afectado, operación, etapa, resultado y motivo, sin exponer contraseñas ni secretos.
5. Mostrará el aviso o modal crítico definido en los anexos anteriores, indicando qué recurso falló, dónde se esperaba encontrarlo, en qué etapa ocurrió y qué acción correctiva se requiere.
6. La ejecución permanecerá detenida. Cerrar el modal no reanudará el proceso.
7. Una vez restaurado el recurso, la aplicación deberá ejecutar nuevamente las validaciones de existencia, acceso, permisos e integridad antes de habilitar la reanudación.

## F.6. Conservación y tratamiento de resultados parciales

La aplicación deberá diferenciar entre resultados completos y confirmados, archivos temporales e incompletos, y resultados inconsistentes.

- **Resultados completos y válidos:** se conservarán y se registrarán como completados. No deberán generarse nuevamente durante la reanudación.
- **Archivos o elementos temporales incompletos:** se descartarán de forma controlada y se regenerarán cuando corresponda.
- **Resultados inconsistentes o cuya validez no pueda comprobarse:** no se considerarán completados. Se deberán poner en revisión o regenerar de forma controlada, evitando sobrescribir silenciosamente un resultado válido.
- **PDF generados:** un PDF final completo y validado se conservará. La reanudación no deberá crear una segunda copia equivalente para la misma unidad de trabajo.
- **Libro Excel y pestañas generadas:** se conservarán las pestañas completas y validadas. La reanudación deberá identificar las pestañas ya procesadas y continuar con las pendientes, sin duplicar pestañas ni repetir información.
- La aplicación deberá utilizar identificadores estables de ejecución y de unidad de trabajo para reconocer resultados existentes y evitar duplicados.

La conservación de resultados parciales no significa que cualquier archivo presente en la carpeta de salida sea válido. Antes de omitir su generación, la aplicación deberá comprobar que corresponde a la ejecución, unidad de trabajo e insumos esperados y que está completo.

## F.7. Recuperación después de un cierre inesperado o apagado del equipo

Al abrir la aplicación, si existe una ejecución que no alcanzó el estado COMPLETADO o CANCELADO:

1. Recuperará el estado persistido de la última ejecución.
2. Identificará el último punto de control confirmado y las unidades de trabajo pendientes.
3. Comprobará los insumos y recursos requeridos para continuar.
4. Comparará las huellas de los insumos con las registradas para esa ejecución.
5. Verificará los resultados parciales que deban conservarse.
6. Si todas las comprobaciones son satisfactorias, dejará la ejecución en estado LISTO PARA REANUDAR y permitirá continuar desde el primer punto pendiente.
7. Si detecta un recurso faltante, inaccesible o modificado, bloqueará la reanudación y mostrará el detalle del problema y la acción requerida.

La recuperación automática del estado al abrir la aplicación no autoriza a iniciar el procesamiento sin validaciones ni implica que la ejecución deba reanudarse sin intervención del usuario. La aplicación recupera y valida; la reanudación del procesamiento se realiza únicamente cuando las condiciones sean satisfactorias y se ejecute la acción correspondiente.

## F.8. Estados de ejecución relacionados con recuperación

Como mínimo, el flujo deberá contemplar los siguientes estados:

- **LISTO:** configuración e insumos validados; todavía no se ha iniciado el procesamiento.
- **EJECUTANDO:** procesamiento en curso.
- **PAUSADO POR INCONSISTENCIA:** procesamiento detenido por pérdida, inaccesibilidad o inconsistencia de un recurso.
- **VALIDANDO RECUPERACIÓN:** comprobación de recursos, huellas y resultados parciales después de una interrupción.
- **LISTO PARA REANUDAR:** validaciones satisfactorias; existe trabajo pendiente y puede reanudarse desde el último punto de control.
- **COMPLETADO:** todas las unidades de trabajo finalizaron correctamente.
- **CANCELADO:** la ejecución fue cancelada explícitamente.

Los cambios de estado deberán quedar registrados. No se deberá pasar a EJECUTANDO si existen validaciones pendientes, errores sin resolver o insumos modificados que estén pendientes de revisión.

## F.9. Reanudación y prevención de duplicados

La reanudación deberá seguir este orden:

1. Recuperar la ejecución y su último punto de control.
2. Validar todos los recursos necesarios para las unidades pendientes.
3. Verificar la identidad e integridad de los insumos.
4. Verificar que los resultados marcados como completados existen y son válidos.
5. Mantener los resultados válidos y excluirlos del reprocesamiento.
6. Identificar la primera unidad de trabajo pendiente.
7. Reanudar desde esa unidad, actualizando los puntos de control a medida que se confirmen nuevos resultados.

La aplicación deberá implementar un comportamiento idempotente: repetir una operación de recuperación o reanudación no deberá duplicar PDFs, pestañas, registros ni otros resultados de una misma unidad de trabajo.

## F.10. Criterios de aceptación

1. **Insumo eliminado durante la ejecución:** la aplicación detiene de forma controlada el procesamiento dependiente, registra el incidente y conserva el último punto de control confirmado.
2. **Insumo restaurado sin cambios:** después de validar existencia, acceso, permisos y huella coincidente, la aplicación permite reanudar desde el primer punto pendiente.
3. **Insumo restaurado con modificaciones:** la aplicación detecta la diferencia de huella, bloquea la reanudación automática y solicita revisión.
4. **Apagado inesperado:** al abrir nuevamente la aplicación, recupera el estado persistido, valida los recursos y determina si puede dejar la ejecución lista para reanudar.
5. **PDF ya generado:** si está completo, validado y asociado a la ejecución, se conserva y no se genera un duplicado.
6. **Pestañas Excel ya generadas:** las pestañas completas y válidas se conservan; la reanudación continúa con las pendientes sin duplicarlas.
7. **Resultado temporal incompleto:** no se marca como completado; se descarta o regenera de forma controlada.
8. **Reanudación repetida:** ejecutar nuevamente la acción de reanudación no duplica resultados ya confirmados.
9. **Recurso aún no disponible:** la ejecución permanece detenida y no permite continuar hasta que las validaciones requeridas sean satisfactorias.
10. **Cancelación:** si el usuario cancela la ejecución interrumpida, el estado queda registrado como CANCELADO y no se reanuda automáticamente en una apertura posterior.

## F.11. Regla general

La aplicación deberá priorizar la integridad de los datos y la trazabilidad sobre la continuidad inmediata del procesamiento. Ante cualquier duda sobre la identidad de un insumo o la validez de un resultado, deberá detener la reanudación automática y solicitar revisión. La regla principal es reanudar desde el último punto confirmado, conservar lo válido y no duplicar resultados.
