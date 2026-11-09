# Guía de instalación y primera puesta en marcha

Aplicativo de escritorio de Fon Prebel (WPF, .NET 8, win-x64). Esta guía cubre cómo
generar el publicado autocontenido, cómo llevarlo a la máquina del trabajo y qué
hay que configurar la primera vez.

Estado: **borrador en construcción**. El proceso de las cinco fases todavía no está
implementado (tareas B1 a B7), así que hoy se pueden probar Configuración, Conexiones,
Métricas, Logs e Historial, pero INICIAR no procesa archivos.

## 1. Qué necesita la máquina de destino

- Windows 10 u 11, 64 bits.
- Permiso de **lectura** en las cuatro rutas del servidor de archivos. Además,
  permiso de **escritura** donde la aplicación va a escribir cuando existan las
  fases: la hoja del mes del consolidado (ruta 1), el archivo del analista de la
  fase 2 (ruta 3) y la carpeta de PDFs (ruta 4). Hoy la aplicación sólo comprueba
  que las rutas existan y se puedan abrir; el permiso de escritura se ejercita
  recién con las fases B1 y B2, que todavía no están implementadas.
- Una cuenta de correo utilizable para los envíos (pendiente de confirmar con TI).
- **No necesita** el SDK de .NET, ni Visual Studio, ni permisos de administrador.

## 2. Generar el publicado (máquina de desarrollo)

Desde la raíz del repositorio, con el SDK de .NET 8 instalado:

```bat
publicar.bat
```

o, si se prefiere PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File tools/publicar.ps1
```

Genera dos cosas:

- `publish\FonPrebel\` — carpeta lista para ejecutar.
- `publish\FonPrebel-win-x64.zip` — el paquete que se copia a la otra máquina.

El script cierra una instancia viva antes de compilar (en Windows una instancia abierta
bloquea el DLL) y verifica que el runtime viaje adentro: si falta `hostfxr.dll`,
`coreclr.dll` o `e_sqlite3.dll`, falla en vez de entregar un paquete incompleto.

`publish/` está ignorado por git: es salida de compilación, no se versiona.

## 3. Instalar en la máquina del trabajo

1. Copiar `FonPrebel-win-x64.zip` (por red, USB o el medio que autorice TI).
2. Descomprimir en una carpeta local, por ejemplo `C:\FonPrebel`.
3. Ejecutar `FonPrebel.exe`.

Notas:

- Es **portátil**: no se instala en `Program Files`, no escribe en el registro y no
  requiere administrador. Desinstalar es borrar la carpeta de la aplicación; los
  datos **no** se borran con eso: quedan en `%LOCALAPPDATA%\FonPrebel` (base,
  credencial cifrada y logs). Para dejarla limpia del todo hay que borrar también
  esa carpeta.
- El ejecutable **no está firmado**, así que Windows puede mostrar el aviso de
  SmartScreen la primera vez ("Más información" → "Ejecutar de todas formas"). Si TI
  exige un binario firmado, eso se resuelve con un certificado de firma de código y
  queda fuera de este alcance.
- Todavía **no hay instalador** (`.msi`/`.msix`): la política de TI sobre instaladores
  y permisos es un pendiente del spec (§5.5). El publicado portátil es la alternativa
  mientras eso no esté definido.

## 4. Dónde guarda los datos

Todo queda en el perfil de Windows del usuario que ejecuta la aplicación:

- Base SQLite: `%LOCALAPPDATA%\FonPrebel\Database\FonPrebel.db`
- Logs: `%LOCALAPPDATA%\FonPrebel\Logs\<fecha>\execution.log` y `errors.log`
- Salida del Excel final: carpeta local `Salida/` (ubicación exacta pendiente, §5.7)

**Nada de eso viaja con la aplicación.** Copiar la carpeta a otra máquina no copia la
configuración ni la contraseña: se cargan una vez en cada máquina.

## 5. Primera puesta en marcha

En la máquina que tiene acceso al servidor de archivos:

1. Abrir **Configuración**.
2. Cargar el correo (servidor, puerto, seguridad, remitente, tiempo de espera), el
   proxy si corresponde, las cuatro rutas del servidor de archivos y los intervalos.
   Cada campo se habilita con el lápiz; al editar una ruta la aplicación comprueba en
   el acto que exista y que se pueda leer o escribir, con el indicador dentro del campo.
3. Registrar la contraseña del correo. Queda cifrada con DPAPI y atada a ese perfil de
   Windows: si se cambia de usuario o de máquina, hay que reingresarla.
4. Guardar. El botón está inactivo hasta que cambie algún valor. Si falta algún
   obligatorio, la aplicación avisa con un diálogo que la información se guarda pero no
   alcanza para ejecutar el aplicativo.
5. Ir a **Conexiones** y usar "Comprobar conexión" en el servidor de archivos: debe
   responder cuántas de las cuatro rutas configuradas existen y se pueden leer. El
   rechequeo automático corre cada 2 minutos por omisión; el intervalo se cambia
   en Configuración.
6. **INICIAR** se habilita recién cuando la configuración obligatoria está completa y la
   credencial es utilizable. Hoy, al pulsarlo, informa que el proceso de las fases está
   pendiente: no procesa archivos todavía.

## 6. Si algo falla

- **"No se puede abrir FonPrebel.dll… bloqueado por .NET Host"** al compilar: quedó una
  instancia viva. `run.bat` la cierra solo; si se compila desde Visual Studio, cerrar la
  ventana de la aplicación antes.
- **La comprobación de conexión dice "Sin comprobar"**: falta configurar esa ruta o la
  credencial. El motivo aparece debajo del estado.
- **La comprobación dice "No responde"**: la ruta existe en la configuración pero no se
  puede abrir con el usuario actual; revisar permisos del recurso compartido.
- Los detalles quedan en `errors.log`; la pantalla **Logs** los muestra sin salir de la
  aplicación.
