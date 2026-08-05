<h1 align="center">MoreWater Object Spy</h1>

<p align="center">
  <strong>Inspector de objetos de UI para QA Automation.</strong><br>
  Captura el objeto en el <em>instante del clic</em> —aunque la ventana cambie o se abra un modal—<br>
  y genera <strong>locators listos para Winium/WinAppDriver</strong>, rankeados por estabilidad y unicidad.
</p>

<p align="center">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4">
  <img alt="C#" src="https://img.shields.io/badge/C%23-WinForms-239120">
  <img alt="Windows" src="https://img.shields.io/badge/Windows-Desktop-0078D6">
  <img alt="Licencia MIT" src="https://img.shields.io/badge/licencia-MIT-blue">
  <img alt="Estado" src="https://img.shields.io/badge/estado-activo-2ea44f">
</p>

---

## ¿Por qué existe?

Inspect.exe, UISpy o Accessibility Insights **pierden el objeto** cuando la aplicación cambia de ventana, abre un modal
o roba el foco — justo lo más común en apps de escritorio reales. Y cuando sí lo encuentran, **solo muestran propiedades**:
el locator hay que armarlo y depurarlo a mano.

**MoreWater Object Spy** toma un *snapshot inmutable* del objeto en el instante del clic (sobrevive aunque la ventana
desaparezca) y te entrega el **locator recomendado**, verificando además que sea **único**.

## Características

| | |
|---|---|
| 🎯 **Captura global** | F8 (al clic) o F9 / "Capturar en 3s" (bajo cursor, para menús que se cierran). |
| 🧩 **3 motores** | UI Automation + MSAA + Win32 — red de seguridad para controles Delphi/VCL. |
| 🏷️ **Locators rankeados** | `By.id` / `By.name` / `By.className` / `By.xpath`, ordenados por estabilidad. |
| 🔢 **Unicidad + índice** | Cuenta coincidencias y, si hay varias, da el **índice correcto** (XPath `[n]` y `findElements().get(i)`). |
| ⬆️ **Promoción inteligente** | Si clicas el texto dentro de un botón, sube solo al control accionable. |
| 🌳 **Árbol de objetos** | Explora toda la ventana con carga perezosa. |
| 🔴 **Resaltado en pantalla** | Dibuja el elemento seleccionado, como Inspect.exe. |
| 💾 **Exportación JSON** | Historial completo de capturas reutilizable. |

## Motores de inspección

| Motor | Expone | Cubre |
|---|---|---|
| **UI Automation** | Name, AutomationId, ControlType, FrameworkId, patterns, árbol | WPF, WinForms, UWP/XAML, Win32 |
| **MSAA** | Role, State, Name, Value, DefaultAction | Delphi/VCL y apps legacy |
| **Win32** | HWND, ClassName, título, PID | Cualquier ventana nativa |

## Requisitos

- .NET SDK **8.0**
- Windows

## Uso

```powershell
cd MoreWaterObjectSpy
dotnet run --project MoreWaterObjectSpy
```

1. **F8** inicia la captura global.
2. Haz clic en cualquier control de la app objetivo.
3. Revisa propiedades (UIA / MSAA / Win32) y copia el **locator recomendado**.
4. Explora la pestaña **Árbol** o exporta el historial a **JSON**.

> Si la app objetivo corre como administrador, ejecuta también la spy como administrador.

## Documentación

- [`docs/DOCUMENTACION_TECNICA.md`](docs/DOCUMENTACION_TECNICA.md) — arquitectura, flujo de captura y lógica de locators.

## Hoja de ruta

- **Grabador de acciones** → genera el script Winium completo (clic → escribir → clic).
- **Multi-framework** → exportar a WinAppDriver, FlaUI y Appium.
- **Extensión a Web** → adaptador DOM (Selenium / Playwright) reutilizando la lógica de ranking, unicidad e índice.
- Validación de locator en vivo, screenshots por captura, CI.

## Licencia

Software de **uso libre** bajo licencia **MIT** — puedes usarlo, modificarlo y distribuirlo;
solo conserva el aviso de copyright. Ver [`LICENSE`](LICENSE).

---

<p align="center">
  MIT © <strong>MazahuaAlan</strong>
</p>