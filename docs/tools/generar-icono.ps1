<#
    generar-icono.ps1 - Regenera MoreWaterObjectSpy\app.ico

    El icono es una insignia azul de marca con una lupa blanca, dibujada por codigo
    en 7 resoluciones (16, 24, 32, 48, 64, 128 y 256 px) dentro de un solo .ico.
    Windows elige la que necesita: 16 en la barra de titulo, 32 en la barra de
    tareas, 256 en la vista de iconos extra grandes del Explorador.

    Uso:
      powershell -ExecutionPolicy Bypass -File docs\tools\generar-icono.ps1

    Solo hace falta ejecutarlo si se cambia el diseno; el .ico ya esta versionado.
    Tras regenerarlo hay que recompilar para que el ejecutable lo tome.

    Notas:
      - Las entradas van comprimidas en PNG. Windows lo admite desde Vista.
      - El grosor del trazo es proporcional al tamano, con un minimo: por debajo
        de 24 px una lupa "fina" se convierte en un borron gris.
      - Este archivo va en ASCII a proposito (PowerShell 5.1 lee un .ps1 sin BOM
        como ANSI y destroza los acentos).
#>

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$RAIZ    = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$DESTINO = Join-Path $RAIZ 'MoreWaterObjectSpy\app.ico'
$TAMANOS = @(16,24,32,48,64,128,256)

# Azules de marca (los mismos del acento claro de la aplicacion)
$AZUL_CLARO = '#4A72B4'
$AZUL_OSC   = '#2E4C7B'

function Dibuja([int]$n) {
  $bmp = New-Object System.Drawing.Bitmap($n, $n, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.InterpolationMode = 'HighQualityBicubic'
  $g.PixelOffsetMode = 'HighQuality'
  $g.Clear([System.Drawing.Color]::Transparent)

  # --- Insignia: cuadrado redondeado con degradado ---
  $m = [Math]::Max(1, [int]([Math]::Round($n * 0.045)))
  $lado = $n - 2*$m
  $r = [int]([Math]::Round($lado * 0.235))
  $d = 2*$r
  $ruta = New-Object System.Drawing.Drawing2D.GraphicsPath
  $ruta.AddArc($m, $m, $d, $d, 180, 90)
  $ruta.AddArc($m + $lado - $d, $m, $d, $d, 270, 90)
  $ruta.AddArc($m + $lado - $d, $m + $lado - $d, $d, $d, 0, 90)
  $ruta.AddArc($m, $m + $lado - $d, $d, $d, 90, 90)
  $ruta.CloseFigure()

  $brocha = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point($m, $m)),
    (New-Object System.Drawing.Point(($m+$lado), ($m+$lado))),
    [System.Drawing.ColorTranslator]::FromHtml($AZUL_CLARO),
    [System.Drawing.ColorTranslator]::FromHtml($AZUL_OSC))
  $g.FillPath($brocha, $ruta)
  $brocha.Dispose(); $ruta.Dispose()

  # --- Lupa blanca ---
  $grosor = [Math]::Max(1.4, $n * 0.093)
  $pluma = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $grosor)
  $pluma.StartCap = 'Round'; $pluma.EndCap = 'Round'

  $cx = $n * 0.435; $cy = $n * 0.415; $rad = $n * 0.205
  $g.DrawEllipse($pluma, [float]($cx-$rad), [float]($cy-$rad), [float]($rad*2), [float]($rad*2))

  $k = 0.7071   # el mango arranca justo en el borde del cristal
  $g.DrawLine($pluma, [float]($cx + $rad*$k), [float]($cy + $rad*$k),
                      [float]($n * 0.775),    [float]($n * 0.775))
  $pluma.Dispose(); $g.Dispose()
  return $bmp
}

$imagenes = @()
foreach ($t in $TAMANOS) {
  $b = Dibuja $t
  $ms = New-Object System.IO.MemoryStream
  $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $imagenes += ,@($t, $ms.ToArray())
  $ms.Dispose(); $b.Dispose()
}

# --- Ensamblado del .ico: ICONDIR + una ICONDIRENTRY por tamano + los PNG ---
$fs = [System.IO.File]::Create($DESTINO)
$w = New-Object System.IO.BinaryWriter($fs)
$w.Write([UInt16]0)                       # reservado
$w.Write([UInt16]1)                       # tipo 1 = icono
$w.Write([UInt16]$imagenes.Count)
$offset = 6 + 16*$imagenes.Count
foreach ($im in $imagenes) {
  $t = [int]$im[0]; $bytes = $im[1]
  $lado = if ($t -ge 256) { 0 } else { $t }   # 0 significa 256 en el formato
  $w.Write([Byte]$lado); $w.Write([Byte]$lado)
  $w.Write([Byte]0)                       # paleta
  $w.Write([Byte]0)                       # reservado
  $w.Write([UInt16]1)                     # planos
  $w.Write([UInt16]32)                    # bits por pixel
  $w.Write([UInt32]$bytes.Length)
  $w.Write([UInt32]$offset)
  $offset += $bytes.Length
}
foreach ($im in $imagenes) { $w.Write($im[1]) }
$w.Flush(); $w.Close(); $fs.Close()

Write-Host ("Generado: {0}" -f $DESTINO)
Write-Host ("  {0} bytes, {1} resoluciones: {2}" -f (Get-Item $DESTINO).Length, $imagenes.Count, ($TAMANOS -join ', '))
Write-Host 'Recompila para que el ejecutable tome el icono nuevo.'
