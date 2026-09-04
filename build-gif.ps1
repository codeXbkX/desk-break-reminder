# build-gif.ps1 - converts an animated SVG into a looping standup.gif that the app plays.
# Requires: Node (with puppeteer-core installed in assets\) + Chrome + ffmpeg. build.ps1 sets these up.

$ErrorActionPreference = 'Stop'
$root    = $PSScriptRoot
$assets  = Join-Path $root 'assets'
$frames  = Join-Path $assets 'frames'
$gifOut  = Join-Path $assets 'standup.gif'

# 1) Locate the source SVG (accepts a few common names).
$svg = $null
foreach ($name in @('stretch.svg', 'JumpingJack.svg', 'Jumping Jack.svg', 'character.svg', 'standup.svg')) {
    foreach ($dir in @($assets, $root)) {
        $cand = Join-Path $dir $name
        if (Test-Path $cand) { $svg = $cand; break }
    }
    if ($svg) { break }
}
if (-not $svg) {
    throw "No source SVG found. Put your animated SVG in '$assets' named 'JumpingJack.svg' (or 'Jumping Jack.svg') and re-run."
}
Write-Host "Source SVG: $svg" -ForegroundColor Cyan

# 2) Locate Chrome.
$chrome = @(
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
    "$env:ProgramFiles (x86)\Microsoft\Edge\Application\msedge.exe",
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $chrome) { throw "Chrome/Edge not found for rendering." }
$env:CHROME_PATH = $chrome
Write-Host "Renderer: $chrome" -ForegroundColor Cyan

# 3) Ensure puppeteer-core is installed.
if (-not (Test-Path (Join-Path $assets 'node_modules\puppeteer-core'))) {
    Write-Host "Installing puppeteer-core..." -ForegroundColor Yellow
    Push-Location $assets
    & npm init -y | Out-Null
    & npm install puppeteer-core@23 | Out-Null
    Pop-Location
}

# 4) Render frames.
if (Test-Path $frames) { Remove-Item $frames -Recurse -Force }
Write-Host "Rendering frames with Chrome..." -ForegroundColor Cyan
Push-Location $assets
& node 'render.js' $svg 'frames' 30 460 1.5
Pop-Location
if (-not (Test-Path (Join-Path $frames 'frame_000.png'))) { throw "Frame rendering failed." }

# 5) Assemble a high-quality looping GIF with ffmpeg.
Write-Host "Encoding standup.gif with ffmpeg..." -ForegroundColor Cyan
# 30 frames captured over a 1.5s loop -> play at 20 fps for real-time, seamless looping.
$fin = Join-Path $frames 'frame_%03d.png'
$vf  = "fps=20,scale=440:-1:flags=lanczos,split[s0][s1];[s0]palettegen=reserve_transparent=1:stats_mode=single[p];[s1][p]paletteuse=alpha_threshold=128:dither=bayer"
& ffmpeg -y -loglevel error -framerate 20 -i $fin -vf $vf -gifflags -offsetting -loop 0 $gifOut
if (-not (Test-Path $gifOut)) { throw "ffmpeg did not produce standup.gif." }

# 6) Copy alongside the built exe and any install location.
$dist = Join-Path $root 'dist'
if (Test-Path $dist) { Copy-Item $gifOut (Join-Path $dist 'standup.gif') -Force }
$installed = Join-Path $env:LOCALAPPDATA 'StandUpReminder'
if (Test-Path $installed) { Copy-Item $gifOut (Join-Path $installed 'standup.gif') -Force }

Write-Host "`nDone: $gifOut" -ForegroundColor Green
Write-Host "Copied next to the app. Run a test reminder to see it." -ForegroundColor Green
