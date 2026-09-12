# build.ps1 - generates the app icon and compiles DeskBreakReminder.exe
# Uses the .NET Framework C# compiler that ships with Windows (no downloads).

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root    = $PSScriptRoot
$src     = Join-Path $root 'src\Program.cs'
$outDir  = Join-Path $root 'dist'
$exePath = Join-Path $outDir 'DeskBreakReminder.exe'
$icoPath = Join-Path $outDir 'app.ico'

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# ---------------------------------------------------------------------------
# 1) Generate a modern gradient icon (PNG-in-ICO, supported on Windows Vista+)
# ---------------------------------------------------------------------------
function New-AppIcon($path) {
    $size = 256
    $bmp  = New-Object System.Drawing.Bitmap $size, $size
    $g    = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $c1 = [System.Drawing.Color]::FromArgb(124, 58, 237)
    $c2 = [System.Drawing.Color]::FromArgb(236, 72, 153)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $c1, $c2, 45
    $path2 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 56; $d = $r * 2
    $path2.AddArc(0, 0, $d, $d, 180, 90)
    $path2.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path2.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $path2.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path2.CloseFigure()
    $g.FillPath($brush, $path2)

    # walking figure (matches the in-app animation)
    $white = [System.Drawing.Color]::White
    $pen = New-Object System.Drawing.Pen $white, 22
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $cx = 122
    $g.DrawLine($pen, $cx+6, 120, $cx, 172)      # torso (slight lean)
    $g.DrawLines($pen, @(                          # back leg (bent)
        (New-Object System.Drawing.Point $cx,172),
        (New-Object System.Drawing.Point ($cx-30),196),
        (New-Object System.Drawing.Point ($cx-40),232)))
    $g.DrawLines($pen, @(                          # front leg (forward)
        (New-Object System.Drawing.Point $cx,172),
        (New-Object System.Drawing.Point ($cx+34),200),
        (New-Object System.Drawing.Point ($cx+58),228)))
    $g.DrawLine($pen, $cx+6, 128, $cx-24, 158)   # back arm
    $g.DrawLine($pen, $cx+6, 128, $cx+40, 150)   # front arm
    $wb = New-Object System.Drawing.SolidBrush $white
    $g.FillEllipse($wb, $cx-8, 64, 46, 46)       # head

    $g.Dispose()

    # Encode PNG then wrap in an ICO container
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $png = $ms.ToArray()
    $ms.Dispose(); $bmp.Dispose()

    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([UInt16]0)      # reserved
    $bw.Write([UInt16]1)      # type = icon
    $bw.Write([UInt16]1)      # count
    $bw.Write([Byte]0)        # width (0 = 256)
    $bw.Write([Byte]0)        # height (0 = 256)
    $bw.Write([Byte]0)        # colors
    $bw.Write([Byte]0)        # reserved
    $bw.Write([UInt16]1)      # planes
    $bw.Write([UInt16]32)     # bpp
    $bw.Write([UInt32]$png.Length)
    $bw.Write([UInt32]22)     # offset
    $bw.Write($png)
    $bw.Flush(); $fs.Close()
}

Write-Host 'Generating app icon...' -ForegroundColor Cyan
New-AppIcon $icoPath

# ---------------------------------------------------------------------------
# 2) Compile
# ---------------------------------------------------------------------------
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) { throw 'C# compiler (csc.exe) not found.' }

Write-Host 'Compiling DeskBreakReminder.exe...' -ForegroundColor Cyan
if (Test-Path $exePath) { Remove-Item $exePath -Force }
$args = @(
    '/nologo',
    '/target:winexe',
    "/out:$exePath",
    "/win32icon:$icoPath",
    '/reference:System.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    $src
)
& $csc @args
if ($LASTEXITCODE -ne 0) { throw "Compilation failed (csc exit $LASTEXITCODE)." }

if (Test-Path $exePath) {
    Write-Host "`nBuild succeeded:" -ForegroundColor Green
    Write-Host "  $exePath"
} else {
    throw 'Build failed - see compiler output above.'
}
