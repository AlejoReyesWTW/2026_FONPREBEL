# D1 — Arquitectura y decisiones técnicas

Estado: decisiones D-01 a D-04 cerradas por el usuario el 2026-11-06. Nada aquí está implementado.
Fuente: `Specs/especificacion-fon-prebel-v0.12.md` §2.1–§2.7, §5.4, §5.5 y Anexo F.

## 1. Plataforma objetivo

| Pieza | Decisión | Motivo |
|---|---|---|
| Lenguaje | C# | §2.1 |
| Runtime | **.NET 8 LTS** (decidido, D-01) | El usuario usa VS 2026 pero exige que el proyecto se abra y ejecute también en **VS 2022**, que no soporta .NET 10. .NET 8 es el LTS compatible con ambos IDEs. Target framework: `net8.0-windows`. |
| UI | WPF + `WPF-UI` de Lepoco (NuGet) | §2.1, obligatorio |
| Patrón | MVVM con `CommunityToolkit.Mvvm` | §2.1 |
| IDE | Visual Studio 2026 (uso habitual del usuario) **y** Visual Studio 2022 | Decisión D-03: el `.sln` y el `.csproj` deben abrir, compilar y ejecutar en ambos, sin degradar nada. |

### 1.1 Decisiones cerradas

| Id | Decisión | Consecuencia |
|---|---|---|
| D-01 | .NET 8 LTS (`net8.0-windows`) | El SDK 8 **ya no hace falta instalarlo**: `global.json` declara `8.0.100` con `rollForward: latestMajor`, y el SDK 10 instalado compila `net8.0-windows` sin problema. El runtime 8 ya está presente (`Microsoft.WindowsDesktop.App 8.0.33`), así que la app corre en 8 tal cual. EF Core queda en 8.x, alineado. Verificado en el proyecto BBVA (§1.2). |
| D-02 | PDF: `PDFsharp` + `MigraDoc` (MIT) | Librería de PDF más usada del ecosistema .NET, documentada, mantenida, sin costo legal. iText queda descartada por AGPL/comercial en un banco. El nivel de cifrado (AES-128/256) se verifica con un prototipo en B4. |
| D-03 | Compatibilidad obligatoria con VS 2022 | El formato del `.sln` y la versión de C# deben ser los que VS 2022 entiende. Nada de funciones de C# 13+ ni de .NET 9/10. |
| D-04 | Sin datos reales todavía | El usuario no tiene las rutas ni los 13 archivos de póliza. Todo el bloque A (esqueleto y backend) se construye sin datos reales; B1 queda bloqueada hasta que existan los archivos. |

### 1.2 Precedente verificado: el proyecto BBVA

El usuario ya construyó un aplicativo de escritorio con el mismo stack en `Desktop/WTW/BBVA/2026_AVALUOS_BANCO_BBVA`. Sirve como precedente probado, no como copia: FONPREBEL tiene otra arquitectura y otras reglas.

Lo que se toma tal cual, porque está verificado que funciona en esta máquina:

| Práctica | Evidencia |
|---|---|
| `global.json` con `"version": "8.0.100"`, `"rollForward": "latestMajor"` | `BotBBVA/global.json`. Es lo que permite compilar `net8.0` con el SDK 10 instalado, sin instalar el SDK 8 |
| `TargetFramework` = `net8.0-windows` + `UseWPF` + `OutputType` = `WinExe` | `src/BotBBVA.Panel/BotBBVA.Panel.csproj` |
| `WPF-UI` **4.3.0** | mismo csproj; es una versión que compila contra `net8.0-windows` |
| `Microsoft.Extensions.Hosting` 8.0.1 | `src/BotBBVA.Servicio/BotBBVA.Servicio.csproj` |
| `System.Security.Cryptography.ProtectedData` 8.0.0 (DPAPI) | mismo csproj |
| xUnit 2.9.3 + `Microsoft.NET.Test.Sdk` 17.14.1 | `tests/BotBBVA.Pruebas/BotBBVA.Pruebas.csproj` |
| Logo como `Resource` dentro de `Assets/` | `BotBBVA.Panel.csproj` incluye `Assets\logoBBVA.png` como recurso |
| `app.manifest` para la aplicación WPF | `ApplicationManifest` en el csproj del panel |

Diferencias que **no** se copian:

- BBVA partió la solución en cuatro proyectos (Contratos, Panel, Servicio, Pruebas). FONPREBEL exige un único `FonPrebel.csproj` con carpetas por capa (§2.2), así que acá va un proyecto de producción más un proyecto de pruebas.
- BBVA incluye un servicio de Windows y Named Pipes. FONPREBEL es una aplicación de escritorio operada por un analista, sin servicio (§1).
- BBVA todavía no incorporó bibliotecas de Excel, PDF ni correo (versión 0.4.0), así que **no hay precedente** para ClosedXML, PDFsharp ni MailKit: esa parte se valida en B1, B4 y B5.

`§5.4` deja la versión de .NET "pendiente". Se propone .NET 10 LTS y se registra como decisión D-01.

## 2. Estructura de la solución (según §2.2, literal)

```text
FonPrebel.sln
src/
└── FonPrebel/
    ├── FonPrebel.csproj          (único proyecto de producción, §2.2)
    ├── Presentation/
    │   ├── Views/  ViewModels/  Commands/  Resources/
    ├── Application/
    │   ├── UseCases/  Services/  Interfaces/  DTOs/
    ├── Domain/
    │   ├── Entities/  Enums/  ValueObjects/  Interfaces/
    ├── Infrastructure/
    │   ├── Excel/  Pdf/  Email/  Database/  Security/  FileStorage/  Logging/
    └── Assets/                    (logo, §2.20)
tests/
└── FonPrebel.Tests/
database/
├── DatabaseDictionary.md
└── exported-scripts/             (sólo documentación, §2.7)
```

Un único proyecto de producción porque §2.2 muestra `FonPrebel.csproj` con carpetas, no varios `.csproj`. Las capas se respetan por convención de carpetas y revisión, no por compilación separada: crear proyectos por capa sería la sobreingeniería que §2.4 prohíbe.

## 3. Paquetes propuestos

| Función | Paquete | Licencia | Nota |
|---|---|---|---|
| UI Fluent | `WPF-UI` (Lepoco) | MIT | §2.1 exige éste y sólo éste |
| MVVM | `CommunityToolkit.Mvvm` | MIT | source generators, menos código |
| DI / hosting | `Microsoft.Extensions.Hosting` | MIT | composición única en `App.xaml.cs` |
| Base local | `Microsoft.EntityFrameworkCore.Sqlite` **8.x** | MIT | §2.1, §2.7. La versión mayor se alinea con .NET 8 de D-01 |
| Excel lectura/escritura | `ClosedXML` | MIT | lee y escribe `.xlsx`, soporta celdas combinadas (Anexo C) |
| PDF con cifrado | `PDFsharp` + `MigraDoc` **6.x** | MIT | **decidido (D-02)**: contraseña de apertura, comunidad amplia, documentación y mantenimiento activo. QuestPDF no cifra; iText es AGPL/comercial |
| Correo | `MailKit` | MIT | SMTP, autenticación, y soporte de proxy HTTP/SOCKS que §5.2 exige |
| Logs | `Serilog` + sinks de archivo | Apache 2.0 | dos archivos por día (§2.17) |
| Cifrado de secretos | `System.Security.Cryptography.ProtectedData` (DPAPI) | BCL | §2.10 |
| Pruebas | `xUnit` | Apache 2.0 | runner `dotnet test` |

Ninguno de estos paquetes está nombrado en el spec. El único con consecuencia legal es el de PDF y ya está decidido en D-02: PDFsharp (MIT). Los demás son estándar del ecosistema y de licencia permisiva; si alguna versión no soporta `net8.0-windows` se ajusta la versión, no el paquete.

## 4. Estrategia de rutas y datos (§2.6, §5.4)

- Raíz única resuelta en tiempo de ejecución: `%LOCALAPPDATA%\FonPrebel\`.
- `Database/FonPrebel.db`, `Logs/YYYY-MM-DD/{execution.log,errors.log}`, `Salida/`.
- Ninguna ruta absoluta en el código ni en la configuración inicial; las rutas del servidor de archivos son parámetros editables que hoy no tienen valor (`§5.1`).
- La base nunca vive junto al ejecutable ni en el servidor compartido.
- SQLite: WAL activado y *busy timeout* parametrizado. Los valores concretos están pendientes (`§2.6`, `§5.4`): se exponen como parámetros, con valor marcado como pendiente, sin inventar.

## 5. Portabilidad: probar acá, usar en el trabajo

El requisito del usuario es que se pruebe en su máquina personal y funcione en la del trabajo. La consecuencia técnica:

1. **Se compila acá, se ejecuta allá.** El paquete se publica autocontenido (`dotnet publish -c Release -r win-x64 --self-contained true`), así que la máquina destino no necesita SDK ni Visual Studio. Esto desacopla la versión de VS de la del runtime.
2. **También se puede compilar en el trabajo**: con D-01 (.NET 8) el proyecto abre y compila tanto en VS 2022 como en VS 2026, así que no hay que elegir una sola máquina de desarrollo.
3. **DPAPI con ámbito `CurrentUser`** implica que los secretos guardados en una máquina no se leen en otra. Al mover la base o el perfil, las credenciales deben reingresarse. Es coherente con §2.10 ("descifrar bajo demanda") y evita llevar secretos entre equipos.
4. **Sin estado en el código**: base SQLite local por equipo, archivos en el servidor compartido, `Salida/` local. Mover la app no mueve datos.
5. **Migraciones al iniciar**: la app aplica migraciones pendientes al arrancar (§2.7), de modo que una versión nueva funciona sobre una base existente sin intervención manual.

## 6. Composición y arranque

- Un único punto de composición en `App.xaml.cs` con `HostBuilder`: servicios de infraestructura, ViewModels y Views registrados explícitamente. Sin contenedores adicionales ni escaneo automático.
- `IDbContextFactory<AppDbContext>` registrado como factoría; cada operación crea y descarta su contexto.
- Excepciones no controladas: se registran en `errors.log` y se traducen a una alerta crítica modal (Anexo E) cuando afectan al proceso; nunca a un cierre silencioso.
- `CancellationToken` atraviesa los casos de uso; la cancelación deja estado consistente (§2.15, Anexo F).

## 7. Pruebas

- Unitarias de reglas de negocio puras: agrupación y suma de Anexo C, máquina de estados, cálculo de progreso, validación de configuración.
- Integrales sobre copias de los archivos de muestra del repositorio en un directorio temporal, nunca sobre los originales.
- Prueba de la regla de 400 líneas (§2.5) como verificación de repositorio.

## 8. Riesgos técnicos identificados

| Riesgo | Consecuencia | Mitigación |
|---|---|---|
| Los 13 archivos de póliza no están en el repositorio | B1 no puede diseñarse | Confirmado por el usuario (D-04): no los tiene. B1 queda bloqueada; el bloque A avanza igual |
| .NET 8 LTS termina su soporte en noviembre de 2026 | Actualización obligatoria de plataforma a mediano plazo | Dejar la migración a .NET 10 como cambio aislado y posterior; no condiciona el diseño actual |
| Los encabezados reales difieren del Anexo A/B | Un mapeo literal rompe la fase 1 y 2 | Resolver por nombre normalizado (sin tildes, sin espacios, sin `#`) y confirmar la lista final |
| PDFsharp puede no ofrecer AES-256 en la versión elegida | El banco podría exigir 256 bits | Prototipo mínimo de cifrado al inicio de B4: generar, cifrar y abrir con la cédula. Si hace falta 256 bits se evalúa entonces, con evidencia |
| Servidor de correo del banco sin confirmar | B5 no puede probarse de verdad | Interfaz lista, prueba en vivo pendiente de TI |
| 400 líneas por archivo con un solo proyecto | Archivos grandes tienden a crecer | Revisión por tarea y script de verificación |

## 9. Hallazgos de la implementación de A1 (verificados, no supuestos)

| Hallazgo | Consecuencia |
|---|---|
| `dotnet new sln` del SDK 10 genera **`.slnx`**, y **Visual Studio 2022 no abre `.slnx`** | D-03 obliga a formato clásico. La solución se crea con `dotnet new sln --format sln` y queda `FonPrebel.sln`. Nunca regenerar con el valor por defecto del SDK 10 |
| WPF-UI 4.3.0 sí publica un target `net8.0-windows7.0` | Compatible con `net8.0-windows`, sin forzar .NET 9/10 |
| `NavigationView` de WPF-UI 4.3.0 **rechaza contenido hijo** (MC3028) | La ventana usa un `Grid` de dos columnas: `NavigationView` a la izquierda y `ContentControl` a la derecha. El logo de marca de agua va detrás del `ContentControl` |
| `NavigationView.ItemInvoked` es un evento enrutado `(object, RoutedEventArgs)`; no existen `NavigationViewItemInvokedEventArgs` ni `InvokedItemContainer` | El ítem se resuelve subiendo por el árbol visual desde `e.OriginalSource` |
| `NavigationView.SelectedItem` tiene el setter **inaccesible** | La sección inicial se resalta con `NavigationViewItem.IsActive = true`, no asignando `SelectedItem` |
| Un proyecto WPF sin `App.xaml` no genera `Main` | `dotnet test` falla con `CS5001` hasta que existe el punto de entrada |

Build y pruebas verificados: `dotnet build FonPrebel.sln` sin errores ni advertencias, `dotnet test FonPrebel.sln` con 6 de 6 pruebas correctas, y la aplicación arranca y permanece en ejecución.

## 10. Hallazgos de A2 y A3 (verificados en ejecución)

| Hallazgo | Consecuencia |
|---|---|
| `Serilog.Sinks.File` 6.0.0 no admite la fecha en la ruta del archivo | La carpeta `Logs/YYYY-MM-DD` exige un sink propio (`DailyFileSink`) que reabre el destino cuando cambia el día del evento |
| `shared: true` + `buffered: false` en el sink de archivo | La UI puede leer `execution.log` mientras la aplicación sigue escribiendo |
| `dotnet new tool-manifest` del SDK 10 escribe el manifiesto en la raíz del repo | Se mueve a `.config/dotnet-tools.json`, que es la ruta convencional |
| `Microsoft.Data.Sqlite` no implementa `GetSchema("Tables")` | Las pruebas de esquema consultan `sqlite_master` |
| `PRAGMA journal_mode` y `PRAGMA busy_timeout` no tienen API en `Microsoft.Data.Sqlite` | Ver la excepción documentada abajo |
| Un contexto nuevo no carga colecciones de navegación sin `Include` | Las pruebas de esquema las piden explícitamente |

### 10.1 Excepción documentada a la §2.7 (SQL en texto)

La §2.7 prohíbe escribir SQL en texto dentro del código. Se cumple en todo el acceso a datos: las consultas son LINQ y el esquema se declara con Fluent API, incluido el `CHECK ("Id" = 1)` de `EmailConfiguration`, que es metadato de esquema.

Queda **una única excepción**, en `Infrastructure/Database/SqlitePragmaInterceptor.cs`: dos sentencias `PRAGMA` como texto (`journal_mode = WAL` y `busy_timeout`). No son consultas de negocio, no leen ni escriben datos de la aplicación, y `Microsoft.Data.Sqlite` no expone ninguna API para fijarlas. Sin ellas no se puede cumplir el requisito del §2.6 de activar WAL y el tiempo de espera ante bloqueo. Se documenta acá para que no parezca una violación inadvertida: si el banco exige cero texto SQL, la alternativa es un `SqliteConnection` con interceptor nativo, que hoy no existe en la librería.
