# Original fictional artwork. Windows GDI+ rasterization; no external assets/font files.
Add-Type -AssemblyName System.Drawing
$taskOutput=Join-Path $PSScriptRoot '../assets/art_r19/freight.png'
$bitmap=New-Object Drawing.Bitmap 1024,552
$graphics=[Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode='AntiAlias'
$graphics.TextRenderingHint='AntiAliasGridFit'
$graphics.Clear([Drawing.Color]::FromArgb(12,29,42))
$cyan=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(73,213,216))
$white=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(238,242,232))
$amber=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(246,170,49))
$dark=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(12,29,42))
$title=New-Object Drawing.Font 'Arial',72,([Drawing.FontStyle]::Bold)
$subtitle=New-Object Drawing.Font 'Arial',28,([Drawing.FontStyle]::Bold)
$small=New-Object Drawing.Font 'Arial',19
$graphics.FillRectangle($cyan,40,40,14,390)
$graphics.FillRectangle($amber,40,465,944,47)
$graphics.DrawString('NORTHLINE',$title,$white,78,91)
$graphics.DrawString('FREIGHT + LOGISTICS',$subtitle,$cyan,87,214)
$graphics.DrawString('CARGO TERMINAL  /  SERVICE DEPOT',$small,$white,89,301)
$graphics.DrawString('KEEP THE CITY MOVING',$subtitle,$white,87,361)
$graphics.DrawString('DELIVERIES  >  LOADING BAYS 01 - 06',$small,$dark,80,470)
$graphics.DrawString('NL / 24',$small,$cyan,820,44)
$bitmap.Save($taskOutput,[Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose();$bitmap.Dispose()
foreach($item in @($cyan,$white,$amber,$dark,$title,$subtitle,$small)){$item.Dispose()}
