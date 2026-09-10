# MoreWater Object Spy — Documentación Técnica

> Inspector de objetos de UI para QA Automation, orientado a generar **locators listos para Winium/WinAppDriver**
> en aplicaciones de escritorio Windows (Delphi/Win32, WPF, WinForms, UWP/WinUI).

> **¿Buscas cómo se usa, no cómo está hecho?** → [`GUIA_VISUAL.md`](GUIA_VISUAL.md), la guía gráfica de cada vista.

---

## 1. Problema que resuelve

Las herramientas tradicionales (Inspect.exe, UISpy, Accessibility Insights, AutoIt Window Info, Spy++) **sí encuentran** los
objetos, pero comparten una limitación crítica para automatizar aplicaciones que **cambian, superponen o destruyen ventanas**
rápidamente (modales, overlays, menús que roban el foco):

```
App de escritorio
   │ clic en un botón
   ▼
Se abre otra ventana / modal
   │
La ventana anterior cambia o desaparece
   ▼
El inspector pierde el objeto → no alcanzas a leer sus propiedades
```

**MoreWater Object Spy** toma un **snapshot inmutable del objeto en el instante exacto del clic**, antes de que la aplicación
procese el evento y la ventana pueda cambiar. El objeto capturado sobrevive aunque su ventana desaparezca.

---

## 2. Arquitectura

```
                 ┌──────────────────────────────────────────────┐
                 │                  MainForm (UI)                │
                 │  Pestaña Captura · Pestaña Árbol · Overlay    │
                 └───────────────┬──────────────────────────────┘
                                 │
                 ┌───────────────▼───────────────┐
                 │        CaptureService          │  orquesta la captura
                 └───────┬───────────────┬────────┘
        hook (hilo rápido)│               │ hilo de fondo
                 ┌────────▼──────┐  ┌─────▼──────────────────────────┐
                 │   MouseHook   │  │  Inspectors (3 motores)        │
                 │  WH_MOUSE_LL  │  │  UIAutomation · MSAA · Win32   │
                 └───────────────┘  └─────┬──────────────────────────┘
                                          │
                        ┌─────────────────▼─────────────────┐
                        │  LocatorGenerator + Uniqueness     │  ranking + índice
                        └─────────────────┬─────────────────┘
                                          │
                                 ┌────────▼────────┐
                                 │  JsonExporter    │
                                 └──────────────────┘
```

### Módulos

| Carpeta | Archivo | Rol |
|---|---|---|
| `Hooks` | `MouseHook.cs` | Hook global de mouse (`WH_MOUSE_LL`). Captura HWND bajo el punto en el instante del clic. |
| `Inspectors` | `Win32Inspector.cs` | Snapshot Win32 **síncrono** (HWND, ClassName, título, PID) en el momento del clic. |
| `Inspectors` | `UIAutomationInspector.cs` | UI Automation: Name, AutomationId, ControlType, patterns, ancestros, bounding. |
| `Inspectors` | `MSAAInspector.cs` | MSAA/IAccessible (Role, State, Name, Value, DefaultAction) — red de seguridad para Delphi. |
| `Services` | `CaptureService.cs` | Orquesta: hook → Win32 → UIA/MSAA → historial → evento a la UI. |
| `Services` | `LocatorGenerator.cs` | Genera y rankea candidatos de locator. |
| `Services` | `UniquenessService.cs` | Cuenta coincidencias en la ventana y calcula el índice real del elemento. |
| `Services` | `TreeService.cs` | Explorador de árbol UIA con carga perezosa. |
| `Infrastructure` | `NativeMethods.cs` | P/Invoke (user32, kernel32, oleacc). |
| `Infrastructure` | `HighlightOverlay.cs` | Rectángulo de resaltado en pantalla (transparente, click-through). |
| `Infrastructure` | `JsonExporter.cs` | Exporta el historial de capturas a JSON. |
| `Core` | `CapturedObject.cs` | Modelo del snapshot (Mouse · UIA · MSAA · Win32 · Candidatos). |

---

## 3. Flujo de captura (el corazón de la herramienta)

```
1. Usuario presiona F8            → se instala el hook global de mouse
2. Usuario hace clic en la app    → WH_MOUSE_LL recibe WM_LBUTTONDOWN
3. En el hilo del hook (rápido):
      · WindowFromPoint(x,y)       → HWND bajo el cursor
      · Win32Inspector.Snapshot    → snapshot Win32 SÍNCRONO (la ventana aún existe)
4. En hilo de fondo (no bloquea el hook):
      · UIAutomationInspector       → propiedades UIA + ancestros
      · MSAAInspector               → Role/State/Name (fallback Delphi)
      · LocatorGenerator.Apply      → candidatos rankeados + unicidad
5. Se agrega al historial → la UI muestra propiedades, locators y resalta en pantalla
```

**Punto clave:** el snapshot Win32 se toma en el hilo del hook, de forma síncrona, *antes* de que la app procese el clic.
La consulta UIA (más lenta) corre después en segundo plano. Si para entonces la ventana ya desapareció, al menos las
propiedades Win32 quedaron congeladas.

El resultado de ese flujo, sobre un control real del Bloc de notas — los tres motores y los locators en una sola pantalla:

![Vista Capturas con un control capturado](img/01-capturas.png)

### Modos de captura
- **F8** — captura al hacer clic (hook global).
- **F9 / botón "Capturar en 3s"** — captura el objeto **bajo el cursor sin clic**. Cuenta regresiva de 3s para posicionar
  el mouse sobre menús/hovers que se cierran al hacer clic.

---

## 4. Los tres motores de inspección

| Motor | Qué expone | Cubre |
|---|---|---|
| **UI Automation (UIA)** | Name, AutomationId, ControlType, FrameworkId, patterns, árbol, bounding | WPF, WinForms, UWP/XAML, Win32 |
| **MSAA (IAccessible)** | Role, State, Name, Value, Description, DefaultAction | Delphi/VCL y apps legacy que UIA no expone bien |
| **Win32 API** | HWND, ClassName, título, PID, proceso, rect | Cualquier ventana nativa |

`FrameworkId` de UIA identifica la tecnología real debajo: `Win32` (Delphi), `WPF`, `WinForm`, `XAML`.

---

## 5. Generador de locators (lógica de decisión)

El generador produce **varios candidatos rankeados por estabilidad y unicidad**, con sintaxis Winium/WinAppDriver
(`By.id`, `By.name`, `By.className`, `By.xpath`).

### Reglas aplicadas

1. **Nunca** usar el título de ventana Win32 como Name del control (en WPF sería el mismo para todos los controles).
2. **WPF sin HWND propio** (`NativeHandle=0`) → no ofrecer HWND como locator.
3. **AutomationId numérico** → se degrada (autogenerado/inestable) y se prefiere `Name`.
4. **Promoción a ancestro accionable** → si capturas un texto/imagen (contenido) dentro de un `Button`, se sube al control
   accionable (con `Invoke` y/o AutomationId) y se recomienda **ese** locator.
5. **Sugerencia de acción por pattern** → `Invoke` → `click()`; `Value` sin `Invoke` → `sendKeys()`.
6. **Análisis de unicidad (A1)** → cuenta cuántos elementos matchean cada locator dentro de la ventana y calcula la
   posición del capturado. Si hay varios iguales, genera un **XPath indexado** con el índice real:
   `(//Edit[@ClassName='DateEdit'])[2]` → coincidencia garantizada.

### Ranking final
```
1º  Locators ÚNICOS   (una sola coincidencia)
2º  Por estabilidad   (Alta > Media > Baja > Volátil)
3º  Orden de generación (desempate)
```

### Ejemplo real (botón con label interno)
Al hacer clic sobre el **texto** "Buscar" dentro de un botón:
```
★ #1 [Alta] AutomationId (promovido desde Text 'Buscar')
     By.id("BtnSearch")
  #2 [Baja] Texto hijo (informativo — normalmente NO es lo que clicas)
     By.name("Buscar")
```

---

## 6. Análisis de unicidad (A1)

`UniquenessService` usa UIA para:
1. Subir a la **ventana top-level** (scope de búsqueda).
2. `FindAll(Descendants, PropertyCondition)` por Name / AutomationId / ClassName.
3. Comparar cada resultado con el elemento capturado (`Automation.Compare`) para obtener su **índice 1-based**.

Con eso, cada candidato se marca como `(única ✓)` o `(N coincidencias, #i)`, y cuando hay duplicados se ofrece el
XPath indexado correcto.

Así se ve el ranking ya resuelto en la UI — puesto, estabilidad, unicidad, confianza, compatibilidad con Winium
y la advertencia concreta de cada candidato:

![Locators rankeados en el panel de propiedades](img/02-arbol.png)

---

## 7. Árbol de objetos (pestaña Árbol)

Explorador jerárquico de la ventana con **carga perezosa**: cada nodo carga sus hijos al expandirse (evita cargar de golpe
cientos de nodos WPF). Al seleccionar un nodo se muestran sus propiedades + locators y se **resalta en pantalla**.
Es una *foto* del árbol en un instante; si la app cambia, se recarga con el botón "Cargar árbol".

El toggle **Vista Raw** alterna el walker: `ControlViewWalker` (la vista limpia, equivalente a lo que recorre Winium)
frente a `RawViewWalker` (todos los elementos intermedios). Desde la vista se puede copiar el bloque de propiedades
del nodo o su locator recomendado, sin pasar por la captura al clic.

---

## 8. Grabador de acciones

`RecorderService` reutiliza el `CaptureService` (cada clic ya produce un `CapturedObject` con sus candidatos) y le
suma un `KeyboardHook` (`WH_KEYBOARD_LL`) que traduce cada tecla con `ToUnicodeEx`, respetando Shift, CapsLock y el
layout del teclado.

La escritura **no** genera un paso por tecla: los caracteres se acumulan en un buffer que se vuelca (`Flush`) cuando
llega el siguiente clic, un Enter/Tab/Escape, o al detener la grabación. El resultado es un paso *Escribir* por campo.

```
clic  →  Flush() del buffer anterior  →  paso Click   (y pasa a ser el objetivo actual)
tecla →  buffer.Append(texto)
Enter →  Flush()  →  paso Key ENTER
```

`ScriptGenerator.ToJavaPageObject` produce declaraciones `By` seguidas de las acciones. Si el locator de un paso no es
único, emite `driver.findElements(by).get(i)` con el índice 0-based que calculó `UniquenessService`.

![Vista Grabar acciones con pasos y script](img/03-grabar.png)

Cada paso guarda su `CapturedObject` completo, así que **doble clic sobre un paso** abre el selector de candidatos y
permite cambiar el locator sin volver a grabar; el script se regenera al vuelo.

---

## 9. Resaltado en pantalla (A3)

`HighlightOverlay` es una ventana **transparente, sin bordes y click-through** (`WS_EX_TRANSPARENT | WS_EX_LAYERED |
WS_EX_NOACTIVATE`) que dibuja un rectángulo rojo sobre el `BoundingRectangle` del elemento (~1.6s). No roba el foco ni
intercepta clics. Elementos sin área visible (contenedores lógicos, offscreen) no se resaltan.

---

## 10. Exportación

Todo el historial de capturas se exporta a **JSON** (Escritorio), con `mouse`, `uiAutomation`, `msaa`, `win32` y los
`candidates` de locator por cada objeto. Reutilizable como insumo para construir scripts o para documentar.

---

## 11. Stack técnico

- **.NET 8** + **C#** + **WinForms** (`UseWPF` habilita `System.Windows.Automation` sin NuGet).
- **P/Invoke**: `user32` (hook, ventanas, hotkeys), `kernel32`, `oleacc` (MSAA).
- Sin dependencias externas de NuGet — 100% APIs de Windows.

---

## 12. Limitaciones conocidas

- **Privilegios**: si la app objetivo corre como administrador y la spy no, el hook/UIA puede fallar → ejecutar la spy
  también como administrador.
- **Unicidad en ventanas enormes**: `FindAll` recorre el árbol; puede tardar un instante en apps con miles de nodos.
- **Delphi puro**: algunos controles VCL no exponen UIA; ahí MSAA/Win32 son el respaldo.
- **El árbol es una foto**: no se refresca en vivo si la app cambia.

---

By **MazahuaAlan** · https://github.com/MazahuaAlan/MoreWaterObjectSpy
