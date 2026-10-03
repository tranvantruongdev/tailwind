# Turns the frames recorded by the TrailerCapture PlayMode test into the README GIF and an MP4 clip.
#   powershell -ExecutionPolicy Bypass -File Tools/run-unity-tests.ps1 -TestPlatform PlayMode -Graphics -TestFilter Tailwind.PlayModeTests.TrailerCapture
#   powershell -ExecutionPolicy Bypass -File Tools/make-gif.ps1
# Needs ffmpeg on PATH (winget install Gyan.FFmpeg).
param(
    [int]$Width = 270,
    [int]$GifFps = 20
)

$ErrorActionPreference = "Stop"
$project = Resolve-Path (Join-Path $PSScriptRoot "..")
$frames = Join-Path $project "Logs/frames/frame_%04d.tga"
$gif = Join-Path $project "docs/tailwind.gif"
$mp4 = Join-Path $project "Logs/tailwind.mp4"

if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) { throw "ffmpeg not found. Install it with: winget install Gyan.FFmpeg" }
if (-not (Test-Path (Join-Path $project "Logs/frames/frame_0000.tga"))) { throw "No frames in Logs/frames. Run the TrailerCapture test first (see the header of this script)." }
New-Item -ItemType Directory -Force (Split-Path $gif) | Out-Null

# Two-pass palette keeps the dusk gradient smooth; diff_mode re-dithers only what moves.
$filter = "fps=$GifFps,scale=${Width}:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle"
& ffmpeg -hide_banner -loglevel error -y -framerate 30 -i $frames -vf $filter -loop 0 $gif
if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed making the GIF" }

& ffmpeg -hide_banner -loglevel error -y -framerate 30 -i $frames -c:v libx264 -pix_fmt yuv420p -crf 20 -movflags +faststart $mp4
if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed making the MP4" }

foreach ($file in @($gif, $mp4)) {
    Write-Host ("{0}  {1:N1} MB" -f $file, ((Get-Item $file).Length / 1MB))
}
