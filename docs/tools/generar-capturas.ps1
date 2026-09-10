<#
    generar-capturas.ps1 - Regenera las imagenes de docs/img/

    Que hace:
      1. Abre el Bloc de notas como aplicacion objetivo y la spy a su izquierda.
      2. Maneja la spy con UI Automation y clics reales para dejar cada vista en un
         estado con datos de verdad (una captura real, un arbol cargado, pasos grabados).
      3. Fotografia cada vista y dibuja los numeritos de la guia visual.

    Uso:
      powershell -ExecutionPolicy Bypass -File docs\tools\generar-capturas.ps1

    Ejecutalo tras subir de version, para que las imagenes muestren la version nueva.
    No toques el raton ni el teclado mientras corre: inyecta entradas reales.

    ---------------------------------------------------------------------------
    OJO, tres trampas que ya costaron caro (no las "simplifiques"):

    a) Este archivo esta en ASCII a proposito. PowerShell 5.1 lee un .ps1 en UTF-8
       sin BOM como ANSI: si buscas un boton por su nombre con tilde ('Cargar arbol
       (ventana bajo cursor, 3s)') no casa con nada y el script falla en silencio.
       Por eso los botones se buscan por fragmento sin acentos (ClickLike).

    b) 'Cargar arbol' usa la ventana bajo el cursor CUANDO TERMINA la cuenta de 3s,
       no cuando se pulsa. Primero el clic, despues mover el raton al objetivo.

    c) SendKeys NO llega al hook WH_KEYBOARD_LL del grabador. Hay que inyectar con
       keybd_event, o los pasos saldran sin la escritura. Y como el Shift inyectado
       no se refleja en el GetKeyboardState que usa el hook, el texto de demo va en
       minusculas: si escribes 'MazahuaAlan' se registrara 'mazahuaalan'.
    ---------------------------------------------------------------------------
#>

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

Add-Type @'
using System;using System.Runtime.InteropServices;
public class W {
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint dx,uint dy,uint d,IntPtr e);
 [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int t,bool r);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern short VkKeyScan(char ch);
 [DllImport("user32.dll")] public static extern void keybd_event(byte vk,byte scan,uint flags,IntPtr extra);
 public static void Click(int x,int y){ SetCursorPos(x,y); System.Threading.Thread.Sleep(150);
   mouse_event(0x0002,0,0,0,IntPtr.Zero); System.Threading.Thread.Sleep(70); mouse_event(0x0004,0,0,0,IntPtr.Zero);
   System.Threading.Thread.Sleep(150); }
 public static void Type(string s){
   foreach(char c in s){
     short v = VkKeyScan(c);
     if (v == -1) continue;
     byte vk = (byte)(v & 0xFF);
     bool shift = (v & 0x100) != 0;
     if (shift) keybd_event(0x10,0,0,IntPtr.Zero);
     keybd_event(vk,0,0,IntPtr.Zero);
     System.Threading.Thread.Sleep(15);
     keybd_event(vk,0,2,IntPtr.Zero);
     if (shift) keybd_event(0x10,0,2,IntPtr.Zero);
     System.Threading.Thread.Sleep(45);
   }
 }
}
'@

$ErrorActionPreference = 'Stop'
$RAIZ = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$OUT  = Join-Path $RAIZ 'docs\img'
New-Item -ItemType Directory -Force -Path $OUT | Out-Null

# Prefiere el single-file publicado; si no existe, cae al build de Debug.
$candidatos = @(
  'MoreWaterObjectSpy\bin\Release\net8.0-windows\win-x64\publish\MoreWaterObjectSpy.exe',
  'MoreWaterObjectSpy\bin\Release\net8.0-windows\win-x64\MoreWaterObjectSpy.exe',
  'MoreWaterObjectSpy\bin\Debug\net8.0-windows\MoreWaterObjectSpy.exe'
)
$EXE = $null
foreach ($c in $candidatos) { $ruta = Join-Path $RAIZ $c; if (Test-Path $ruta) { $EXE = $ruta; break } }
if (-not $EXE) { throw 'No encontre el ejecutable. Compila primero (dotnet build o dotnet publish).' }
Write-Host "Ejecutable: $EXE"

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]::Descendants
$CO = [System.Windows.Automation.Condition]::TrueCondition

# ---------- Aplicacion objetivo: Bloc de notas, a la derecha ----------
$np = Start-Process notepad -PassThru
$nh = [IntPtr]::Zero; $i = 0
while ($nh -eq [IntPtr]::Zero -and $i -lt 40) { Start-Sleep -Milliseconds 300; $np.Refresh(); $nh = $np.MainWindowHandle; $i++ }
[W]::MoveWindow($nh, 1190, 90, 700, 620, $true) | Out-Null
Start-Sleep -Milliseconds 500

# ---------- La spy, a la izquierda y a tamano fijo (las imagenes salen 1120x720) ----------
$p = Start-Process $EXE -PassThru
$h = [IntPtr]::Zero; $i = 0
while ($h -eq [IntPtr]::Zero -and $i -lt 60) { Start-Sleep -Milliseconds 500; $p.Refresh(); $h = $p.MainWindowHandle; $i++ }
if ($h -eq [IntPtr]::Zero) { throw 'La spy no arranco' }
[W]::MoveWindow($h, 40, 90, 1120, 720, $true) | Out-Null
Start-Sleep -Milliseconds 800
$root = $AE::FromHandle($h)

function All { $root.FindAll($TS, $CO) }
function ById($id) { foreach ($e in All) { if ($e.Current.AutomationId -eq $id) { return $e } }; $null }
function BtnLike($frag) { foreach ($e in All) { if ($e.Current.Name -like "*$frag*" -and $e.Current.ControlType.ProgrammaticName -like '*Button*') { return $e } }; $null }
function ClickEl($e) { $r = $e.Current.BoundingRectangle; [W]::Click([int]($r.X + $r.Width/2), [int]($r.Y + $r.Height/2)) }
function ClickLike($frag) { $b = BtnLike $frag; if (-not $b) { throw "no encontre boton con '$frag'" }; ClickEl $b }
function Nav($id) { ClickEl (ById $id); Start-Sleep -Milliseconds 900 }
function Shot($file) {
  Start-Sleep -Milliseconds 400
  $r = $root.Current.BoundingRectangle
  $bmp = New-Object System.Drawing.Bitmap([int]$r.Width, [int]$r.Height)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
  $bmp.Save((Join-Path $OUT $file), [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  Write-Host "  $file"
}

Write-Host '1. Vista Capturas, con un control real de Notepad'
ClickLike 'Iniciar captura'
Start-Sleep -Milliseconds 600
[W]::Click(1500, 300)                      # clic en el area de texto: eso es lo que captura
Start-Sleep -Milliseconds 900
ClickLike 'Detener captura'
Start-Sleep -Milliseconds 700
$lst = ById 'LstHistorial'
$items = $lst.FindAll([System.Windows.Automation.TreeScope]::Children, $CO)
if ($items.Count -gt 0) { ClickEl $items[0] }
Start-Sleep -Milliseconds 900
Shot '01-capturas.png'

# El tema oscuro se fotografia AQUI, con el estado aun limpio (una sola captura y el
# mensaje correcto). Al final del guion el panel arrastraria estado del grabador.
Write-Host '2. Tema oscuro'
$sw = ById 'ThemeSwitch'
ClickEl $sw
Start-Sleep -Milliseconds 1200
Shot '04-tema-oscuro.png'
ClickEl $sw
Start-Sleep -Milliseconds 1000

Write-Host '3. Vista Arbol, con el arbol de Notepad'
Nav 'NavArbol'
ClickLike 'Cargar'
Start-Sleep -Milliseconds 400
[W]::SetCursorPos(1500, 300) | Out-Null    # ver trampa (b): el cursor cuenta al final
Start-Sleep -Seconds 5
$nodos = @(); foreach ($x in All) { if ($x.Current.ControlType.ProgrammaticName -like '*TreeItem*') { $nodos += $x } }
Write-Host "   nodos cargados: $($nodos.Count)"
if ($nodos.Count -eq 0) { throw 'El arbol salio vacio: revisa que el cursor acabe sobre Notepad' }
if ($nodos.Count -gt 1) { ClickEl $nodos[1] } else { ClickEl $nodos[0] }
Start-Sleep -Milliseconds 1000
Shot '02-arbol.png'

Write-Host '4. Vista Grabar, con pasos reales'
Nav 'NavGrabar'
ClickLike 'Grabar'
Start-Sleep -Milliseconds 800
[W]::SetForegroundWindow($nh) | Out-Null
[W]::Click(1500, 300)
Start-Sleep -Milliseconds 600
[W]::Type('usuario.qa')                    # minusculas: ver trampa (c)
Start-Sleep -Milliseconds 900
[W]::Click(1500, 360)
Start-Sleep -Milliseconds 600
[W]::Type('automation')
Start-Sleep -Milliseconds 1000
ClickLike 'Detener'
Start-Sleep -Milliseconds 1200
Shot '03-grabar.png'

Write-Host '5. Acerca de'
Nav 'NavAcerca'
Shot '05-acerca.png'

Stop-Process -Id $p.Id -Force
Stop-Process -Id $np.Id -Force -ErrorAction SilentlyContinue

# ---------------------------------------------------------------------------
# Numeritos de la guia visual. Las coordenadas son de la imagen (1120x720) y
# estan elegidas para caer en zonas vacias. Si cambia el layout de una vista,
# ajusta aqui y vuelve a mirar el PNG resultante.
# ---------------------------------------------------------------------------
Write-Host '6. Anotando las imagenes de la guia'
$ROJO = [System.Drawing.ColorTranslator]::FromHtml('#EF4B4C')   # rojo de marca

function Anotar([string]$src, [string]$dst, [array]$puntos) {
  $img = [System.Drawing.Image]::FromFile((Join-Path $OUT $src))
  $bmp = New-Object System.Drawing.Bitmap($img.Width, $img.Height)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.TextRenderingHint = 'ClearTypeGridFit'
  $g.DrawImage($img, 0, 0, $img.Width, $img.Height)
  $img.Dispose()

  $relleno = New-Object System.Drawing.SolidBrush($ROJO)
  $borde   = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 2.5)
  $texto   = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
  $fuente  = New-Object System.Drawing.Font('Segoe UI', 12, [System.Drawing.FontStyle]::Bold)
  $fmt = New-Object System.Drawing.StringFormat
  $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
  $r = 15

  foreach ($pt in $puntos) {
    $n = $pt[0]; $x = [int]$pt[1]; $y = [int]$pt[2]
    $rect = New-Object System.Drawing.Rectangle(($x - $r), ($y - $r), (2 * $r), (2 * $r))
    $g.FillEllipse($relleno, $rect)
    $g.DrawEllipse($borde, $rect)
    $caja = New-Object System.Drawing.RectangleF(($x - $r), ($y - $r + 1), (2 * $r), (2 * $r))
    $g.DrawString([string]$n, $fuente, $texto, $caja, $fmt)
  }

  $bmp.Save((Join-Path $OUT $dst), [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  Write-Host "  $dst"
}

# 1 navegacion | 2 estado | 3 acciones | 4 historial | 5 detalle | 6 locator | 7 tema
Anotar '01-capturas.png' '01-capturas-guia.png' @(
  @(1, 215, 150), @(2, 1080, 82), @(3, 1080, 132), @(4, 545, 190),
  @(5, 1085, 190), @(6, 1085, 660), @(7, 215, 572)
)
# 1 mensajes | 2 acciones | 3 vista raw | 4 arbol | 5 propiedades
Anotar '02-arbol.png' '02-arbol-guia.png' @(
  @(1, 1080, 82), @(2, 1045, 132), @(3, 248, 166), @(4, 640, 400), @(5, 1085, 213)
)
# 1 estado | 2 acciones | 3 pasos | 4 script
Anotar '03-grabar.png' '03-grabar-guia.png' @(
  @(1, 1080, 82), @(2, 1000, 132), @(3, 605, 195), @(4, 1085, 190)
)

Write-Host ''
Write-Host "Listo. Imagenes en $OUT"
Write-Host 'Revisa los PNG antes de commitear: si cambio el layout, los numeritos pueden tapar texto.'
