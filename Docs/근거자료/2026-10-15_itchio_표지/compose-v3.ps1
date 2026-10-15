Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$assetDirectory = $PSScriptRoot
$projectDirectory = [IO.Path]::GetFullPath((Join-Path $assetDirectory '..\..\..'))
$background = [Drawing.Bitmap]::FromFile((Join-Path $assetDirectory 'ScrollHunter_Itch_Background_v3.png'))
$fontCollection = New-Object Drawing.Text.PrivateFontCollection
$fontCollection.AddFontFile((Join-Path $projectDirectory 'Assets\_Project\Fonts\Cinzel\Cinzel-Bold.ttf'))
$fontFamily = $fontCollection.Families[0]
$format = [Drawing.StringFormat]::GenericTypographic.Clone()
$overlay = New-Object Drawing.Bitmap($background.Width, $background.Height)
$graphics = [Drawing.Graphics]::FromImage($overlay)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$titleBounds = @()

foreach ($line in @(@{Text='SCROLL';Top=62}, @{Text='HUNTER';Top=185})) {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddString($line.Text, $fontFamily, [int][Drawing.FontStyle]::Regular, 150, [Drawing.PointF]::new(0,0), $format)
    $bounds = $path.GetBounds()
    $transform = New-Object Drawing.Drawing2D.Matrix
    $transform.Translate(($background.Width - $bounds.Width)/2 - $bounds.X, $line.Top - $bounds.Y)
    $path.Transform($transform)
    $bounds = $path.GetBounds()
    $titleBounds += [pscustomobject]@{Text=$line.Text;X=$bounds.X;Y=$bounds.Y;Width=$bounds.Width;Height=$bounds.Height}
    $shadow = $path.Clone()
    $shadowOffset = New-Object Drawing.Drawing2D.Matrix
    $shadowOffset.Translate(0,4)
    $shadow.Transform($shadowOffset)
    $shadowPen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(65,8,10,9), 11)
    $shadowPen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawPath($shadowPen, $shadow)
    $outline = New-Object Drawing.Pen([Drawing.Color]::FromArgb(210,24,21,14), 3)
    $outline.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawPath($outline, $path)
    $gold = New-Object Drawing.Drawing2D.LinearGradientBrush($bounds, [Drawing.Color]::FromArgb(255,246,225,165), [Drawing.Color]::FromArgb(255,220,183,102), [Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $graphics.FillPath($gold, $path)
    $gold.Dispose(); $outline.Dispose(); $shadowPen.Dispose(); $shadowOffset.Dispose(); $shadow.Dispose(); $transform.Dispose(); $path.Dispose()
}

$goldPen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(235,219,184,111), 2)
$finePen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(150,186,145,70), 1)
$midpoint = $background.Width / 2
$graphics.DrawLine($goldPen, [single]($midpoint-345), 312, [single]($midpoint-28), 312)
$graphics.DrawLine($goldPen, [single]($midpoint+28), 312, [single]($midpoint+345), 312)
$graphics.DrawLine($finePen, [single]($midpoint-345), 317, [single]($midpoint-28), 317)
$graphics.DrawLine($finePen, [single]($midpoint+28), 317, [single]($midpoint+345), 317)
foreach ($item in @(@{X=$midpoint;R=8}, @{X=$midpoint-21;R=5}, @{X=$midpoint+21;R=5})) {
    $points = [Drawing.PointF[]]@([Drawing.PointF]::new($item.X,312-$item.R), [Drawing.PointF]::new($item.X+$item.R,312), [Drawing.PointF]::new($item.X,312+$item.R), [Drawing.PointF]::new($item.X-$item.R,312))
    $graphics.DrawPolygon($goldPen, $points)
}
$goldPen.Dispose(); $finePen.Dispose(); $graphics.Dispose()
$overlay.Save((Join-Path $assetDirectory 'ScrollHunter_Title_Layer_v3.png'), [Drawing.Imaging.ImageFormat]::Png)
$master = New-Object Drawing.Bitmap($background.Width, $background.Height)
$compositor = [Drawing.Graphics]::FromImage($master)
$compositor.DrawImageUnscaled($background,0,0)
$compositor.DrawImageUnscaled($overlay,0,0)
$compositor.Dispose()

foreach ($export in @(@{Width=1260;Height=1000;Name='ScrollHunter_Itch_Cover_v3.png'}, @{Width=630;Height=500;Name='ScrollHunter_Itch_Cover_v3_630x500.png'})) {
    $bitmap = New-Object Drawing.Bitmap($export.Width,$export.Height)
    $canvas = [Drawing.Graphics]::FromImage($bitmap)
    $canvas.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $canvas.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $targetRatio = $export.Width / $export.Height
    $cropWidth = [Math]::Min($master.Width, $master.Height * $targetRatio)
    $cropHeight = $cropWidth / $targetRatio
    $canvas.DrawImage($master, [Drawing.Rectangle]::new(0,0,$export.Width,$export.Height), [single](($master.Width-$cropWidth)/2), [single](($master.Height-$cropHeight)/2), [single]$cropWidth, [single]$cropHeight, [Drawing.GraphicsUnit]::Pixel)
    $canvas.Dispose()
    $bitmap.Save((Join-Path $assetDirectory $export.Name),[Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}
$titleBounds | ConvertTo-Json
$master.Dispose(); $overlay.Dispose(); $background.Dispose(); $format.Dispose(); $fontFamily.Dispose(); $fontCollection.Dispose()
