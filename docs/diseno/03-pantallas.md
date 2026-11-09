# D3 — Diseño de pantallas

Estado: propuesta de diseño. No hay XAML escrito.
Fuente: `Specs/especificacion-fon-prebel-v0.11.md` §2.14, §2.17, §2.18, §2.19, §2.20, §2.21, §2.22, Anexo D, Anexo E.
Stack visual: WPF + **WPF-UI de Lepoco** (Fluent), MVVM con CommunityToolkit.Mvvm, .NET 8. Un único proyecto; las vistas viven en `Presentation/Views`.

## A. Principios visuales (§2.20)

| Regla del spec | Cómo se implementa |
|---|---|
| Fondo blanco | `ApplicationBackgroundBrush`; el tema oscuro opcional cambia sólo el diccionario de recursos |
| Logo grande como marca de agua | `Image` con `Opacity` bajo (≈0.06) detrás del contenido de la ventana principal, `IsHitTestVisible=False`. El logo **no se inventa**: el formato definitivo está pendiente (§2.20, §5.6) |
| Componentes ligeramente transparentes | Tarjetas con `Opacity` ≈0.92 sobre `CardBackgroundFillColorDefaultBrush` |
| Aspecto Fluent | Sólo controles de WPF-UI, nunca `Window`/`Button` crudos cuando existe el equivalente |

Mapa de controles por patrón:

| Patrón | Control WPF-UI |
|---|---|
| Barra lateral de navegación | `NavigationView` con `PaneDisplayMode="Left"` |
| Aviso informativo (config incompleta, fases) | `InfoBar` |
| Modal y confirmaciones | `ContentDialog` |
| Progreso con porcentaje | `ProgressBar` + `TextBlock` |
| Actividad indeterminada | `ProgressRing` |
| Punto de estado de conexión | `Ellipse` de 10 px, verde/amarillo/rojo |
| Aviso efímero al terminar una fase | `Snackbar` (toast, abajo a la derecha) |
| Campos y validación | `TextBox` + `ProgressRing` de 12 px a la derecha + borde verde/rojo |

## B. Mapa de navegación (§2.19)

Orden obligatorio de la barra lateral:

```text
[logo]
  Ejecución          ← sección por defecto al abrir
  Historial
  Logs
  Configuración
  Métricas
  Conexiones
  ─────────────
  Inicio de sesión   ← último; deshabilitado hoy
```

## C. Inventario de pantallas

| Id | Nombre | Sección | Propósito | Estado |
|---|---|---|---|---|
| V1 | Ejecución | Ejecución | Operar el lote: iniciar, reanudar, seguir el avance | diseñada |
| V2 | Historial | Historial | Consultar lotes anteriores, ejecuciones y resultados | diseñada |
| V3 | Logs | Logs | Leer `execution.log` y `errors.log` sin salir de la app | diseñada |
| V4 | Configuración | Configuración | Editar y validar todos los parámetros | diseñada |
| V5 | Métricas | Métricas | Indicadores acumulados calculados desde SQLite | pendiente de indicadores definitivos (§2.22) |
| V6 | Conexiones | Conexiones | Probar servidor de archivos y correo | diseñada |
| V7 | Inicio de sesión | Inicio de sesión | Placeholder de la etapa final | no funcional a propósito |
| V8 | Seguimiento en vivo | Modal | Detalle por jubilado de las fases 2-4 | propuesta a validar |
| V9 | Alerta crítica | Modal | Detener y explicar un fallo bloqueante | diseñada |
| V10 | Toast de fase | Snackbar | Avisar el fin de cada fase | diseñada |

## D. Fichas

### V1 — Ejecución

**Propósito.** Único lugar donde el analista inicia, reanuda y observa el proceso (§2.18).

**Layout.**
```text
┌──────────────────────────────────────────────────────────┐
│ Lote activo: 2026-11-002    [estado del lote]            │
│                                                          │
│  ①──②──③──④──⑤   ● activa con spinner                   │
│                                                          │
│  General   [██████████░░░░░░░]  42 %                     │
│  Fase 1    [████████████████]  100 %                     │
│  Fase 2    [███████░░░░░░░░░]   38 %                     │
│  Fase 3    [████░░░░░░░░░░░░]   21 %                     │
│  Fase 4    [██░░░░░░░░░░░░░░]   12 %                     │
│                                                          │
│  [ INICIAR ]  [ REANUDAR ]  [ CANCELAR ]                 │
│  ┌─ InfoBar (si falta configuración) ──────────────────┐ │
└──────────────────────────────────────────────────────────┘
```

**Controles y datos.** Indicador de fases con un punto por fase y spinner en la activa (§2.18); barra global y por fase (§2.18); identificador del lote activo (§2.18); las cifras salen de `PhaseExecution.ProcessedCount/TotalCount`, nunca de contadores en memoria.

**Estados.**
- *Vacío*: sin lote activo; INICIAR habilitado sólo si la configuración está completa (V4/F).
- *Cargando*: al abrir, consulta el estado persistido antes de mostrar botones (§2.8 regla 4).
- *En ejecución*: INICIAR deshabilitado, CANCELAR habilitado, barras avanzando.
- *Esperando confirmación*: al terminar la fase 1 y la fase 2, el proceso se detiene y pide confirmación del analista antes de continuar (§3 fases 1 y 2).
- *Lote reanudable*: REANUDAR visible, con el aviso de mismo lote (§2.8 reglas 4 y 5).
- *Error bloqueante*: se abre V9 encima.

**Acciones.** INICIAR (crea lote nuevo y ejecución 1); REANUDAR (crea ejecución n+1 del mismo lote); CANCELAR (deja estado terminal consistente, Anexo F); cada confirmación de fase avanza el proceso.

**Reglas de negocio visibles.** "No es un bot desatendido: un analista opera la aplicación y confirma los pasos entre fases" (§1). Nunca se muestra un correo no confirmado como enviado (§2.18).

### V2 — Historial

**Propósito.** Consultar lotes anteriores sin tocar el lote activo (§2.8 regla 10).

**Layout.** Tabla maestra de lotes (código, periodo, estado, ejecuciones, fecha) → panel de detalle con la lista de `BatchRecipient`: cédula, nombre, estado del PDF, estado del envío, motivo del fallo.

**Estados.** *Vacío* (sin lotes), *cargando*, *seleccionado*, *error de lectura*. Sólo lectura: no hay acción de borrar (§2.8 regla 9).

**Reglas visibles.** Los lotes históricos no se sobrescriben ni se eliminan; se listan los estados pendiente / enviado / fallido / incierto con etiquetas distintas.

### V3 — Logs

**Propósito.** Ver las dos bitácoras dentro de la aplicación (§2.17).

**Layout.** Dos paneles lado a lado: `execution.log` y `errors.log`. Barra superior con fecha (carpeta `YYYY-MM-DD`), filtro por nivel, búsqueda de texto y botón de refresco. Cada línea muestra fecha/hora, nivel, lote, ejecución, fase y resultado.

**Estados.** *Vacío* (día sin actividad), *cargando*, *error de lectura* (archivo bloqueado), *auto-refresco activo*.

**Reglas visibles.** Los logs nunca están en SQLite y nunca contienen secretos ni datos sensibles (§2.17).

### V4 — Configuración

**Propósito.** Editar todos los parámetros y credenciales; es la condición para habilitar INICIAR (§2.9, Anexo D).

**Layout.** Secciones colapsables: Correo · Proxy · Rutas del servidor de archivos · Intervalos y umbrales · Credenciales. Cada campo con su etiqueta del spec y **vacío** hasta que exista un valor confirmado.

**Controles.** Correo: servidor, puerto, seguridad, remitente, cuenta. Proxy: interruptor + tipo + host + puerto. Rutas: ruta 1 a ruta 4 con botón de examinar. Intervalos: verificación automática (`2` minutos, único valor que el spec confirma), umbral de conexión lenta (vacío), workers (vacío). Credenciales: contraseña enmascarada, botón de ojo, lápiz para editar con anterior + nueva + confirmación (§2.10.1).

**Estados.** *Sin configurar* (primera ejecución: aviso de configuración inicial pendiente), *parcial*, *validando* (spinner por campo), *válido* (borde verde), *inválido* (borde rojo y mensaje concreto), *secreto expuesto* (si el perfil de Windows cambió y DPAPI no puede descifrar, la credencial debe reingresarse).

**Reglas visibles.** La contraseña guardada nunca se muestra (§2.10.1). Ningún parámetro aparece con un valor inventado. Cambiar un valor invalida el gate de INICIAR hasta revalidar (Anexo D).

### V5 — Métricas

**Propósito.** Indicadores históricos y acumulados, siempre calculados desde SQLite (§2.22).

**Layout.** Fila de tarjetas filtrables por periodo y por lote: lotes procesados, jubilados notificados, % de éxito con numerador y denominador explícitos, fallos por causa, tiempo medio por fase.

**Estado.** Diseño estructural listo; **los indicadores definitivos, filtros, gráficos y periodos de comparación están pendientes** (§2.22, §5.6). Se implementa el marco y se dejan los indicadores vacíos hasta que se definan.

### V6 — Conexiones

**Propósito.** Probar la infraestructura antes de arrancar (§2.14).

**Layout.** Dos tarjetas: *Servidor de archivos* y *Correo*, cada una con `Comprobar conexión`, su punto de estado y su mini-log. Abajo, el estado del rechequeo automático.

**Estados.** Verde (responde), amarillo (lento), rojo (no responde), y *comprobando*. Rechequeo automático **cada 2 minutos** en segundo plano, sin congelar la UI. El umbral en milisegundos para "lento" **está pendiente** (§2.14).

### V7 — Inicio de sesión

**Propósito.** Reservar el lugar de la etapa final (§2.23).

**Layout.** Pantalla con el logo, campos de usuario y contraseña deshabilitados y un texto visible: módulo de inicio de sesión no habilitado. **No se implementa** hasta que se ordene y exista la base central.

### V8 — Seguimiento en vivo (propuesta a validar)

**Propósito.** Ver el detalle por jubilado de las fases 2, 3 y 4 mientras corren.

**Layout.** Modal con tres columnas — PDF, correo y resultado — y una fila por destinatario, con contadores por estado arriba. Se abre desde V1 y no bloquea el proceso.

**Estado.** La spec lo menciona (§2.22, §5.6) pero **no lo define**; por eso es propuesta. Requiere confirmación del funcional antes de implementarse.

### V9 — Alerta crítica modal (Anexo E)

**Propósito.** Detener el proceso y explicar con precisión qué pasó.

**Estructura obligatoria**, en este orden y con estos rótulos:

```text
┌─ PROCESO DETENIDO — ARCHIVO NO DISPONIBLE ─────────────┐
│  [!] icono de alerta, jerarquía roja/naranja            │
│  Qué ocurrió:            ...                            │
│  Dónde:                  [ruta del archivo]             │
│  Qué estaba haciendo:    [etapa del proceso]            │
│  Por qué se detuvo:      [detalle del error detectado]  │
│  Qué debe hacer:         ...                            │
│  Estado del proceso:     ...                            │
│                          [ Entendido ]  [ Configuración ]│
└────────────────────────────────────────────────────────┘
```

**Regla dura.** Cerrar la alerta **no reanuda** el proceso (Anexo E). Reanudar exige la acción explícita del analista en V1.

### V10 — Toast de fase

Al terminar cada fase, un `Snackbar` abajo a la derecha con el nombre de la fase y el resultado, "si es técnicamente posible" (§2.21).

## E. Validación por campo (Anexo E)

Componente reutilizable `ValidatedField`:

1. Al salir del campo o al cambiar el valor se dispara la validación en segundo plano y aparece un `ProgressRing` de 12 px junto al campo.
2. Al terminar: borde y mensaje **verde** si es válido, **rojo** con el motivo concreto si no lo es. Nunca un "campo inválido" genérico.
3. Cada cambio posterior vuelve a validar: el estado anterior no se conserva.
4. Un campo inválido o vacío obligatorio deshabilita las acciones que dependen de él.

## F. Gate del botón INICIAR (Anexo D)

| Condición | INICIAR |
|---|---|
| Falta cualquier parámetro obligatorio | deshabilitado |
| Hay un parámetro presente pero inválido | deshabilitado |
| Hay un secreto que no se puede descifrar en este perfil | deshabilitado |
| Todo válido, sin lote activo | **habilitado** |
| Ya hay un lote en ejecución | deshabilitado |
| Un valor se invalida durante la edición | se vuelve a deshabilitar |

Aviso literal, en tono suave amarillo/naranja:

> "Completa todos los campos obligatorios de Configuración para habilitar el inicio del proceso."

## G. Pendientes visuales heredados del spec

| Pendiente | Sección | Impacto en la UI |
|---|---|---|
| Nombre y formato del archivo del logo | §2.20, §5.6 | Se usa el PNG del repo; el definitivo no está definido |
| Umbral de conexión lenta en ms | §2.14, §5.6, §5.7 | El punto amarillo no tiene umbral hasta que se defina |
| Indicadores, filtros y gráficos de métricas | §2.22, §5.6 | V5 queda como marco con indicadores vacíos |
| Fórmula del progreso general | §2.16, §5.6 | La barra global muestra "pendiente de fórmula" en vez de un porcentaje inventado |
| Detalles visuales de Historial y Configuración | §5.6 | Se implementa la estructura propuesta |
| Diseño del PDF | §3 | Afecta B4, no estas pantallas |
| Modal de seguimiento | §2.22 | V8 queda como propuesta a validar |
