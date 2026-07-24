# MoreWater Object Spy

Herramienta de inspección de objetos de UI para **QA Automation con Winium**, para cualquier aplicación
de escritorio Windows: **Delphi/Win32**, **WPF**, **WinForms** y **UWP/WinUI**.

Captura el objeto bajo un clic global —aunque la ventana cambie o se abra un modal— y genera
**locators listos para Winium/WinAppDriver** (`By.id`, `By.name`, `By.className`, `By.xpath`),
rankeados por estabilidad.

## Motores de inspección
- **UI Automation (UIA)** — WPF, WinForms, UWP/XAML, Win32 (reporta `FrameworkId`).
- **MSAA (IAccessible)** — red de seguridad para Delphi/VCL cuando UIA no expone el control.
- **Win32 API** — snapshot inmediato (HWND, ClassName, título, PID) en el instante del clic.

## Características
- Captura global por clic (F8) y captura con cuenta regresiva / bajo cursor (F9) para menús que se cierran.
- **Locators rankeados** por estabilidad con advertencias (multiplicidad, idioma, volatilidad del HWND).
- **Promoción a ancestro accionable**: si clicas el texto dentro de un botón, sube al botón real.
- Sugerencia de acción por *pattern* (`Invoke`→`click()`, `Value`→`sendKeys()`).
- Historial de capturas + exportación a **JSON**.

## Requisitos
- .NET SDK 8.0
- Windows

## Uso
```powershell
cd MoreWaterObjectSpy
dotnet run --project MoreWaterObjectSpy
```
1. **F8** inicia la captura global.
2. Haz clic en cualquier control de la app objetivo.
3. Revisa propiedades (UIA / MSAA / Win32) y copia el locator recomendado.
4. Exporta el historial a JSON.

---
By **MazahuaAlan** · https://github.com/MazahuaAlan
