$ErrorActionPreference = 'Stop'
$projectRoot = 'C:\Users\MBC-501-19\Documents\ScrollHunter'
$captureFolder = Join-Path $env:TEMP 'ScrollHunter_ArcaneBarrierVfx_20261020\capture-reviewed'
$encoderPath = Join-Path $env:TEMP 'ScrollHunter_HonestTrailer_20261019\ffmpeg.exe'
$report = Get-Content -LiteralPath (Join-Path $captureFolder 'report.json') -Raw | ConvertFrom-Json
if ($report.failed.Count -gt 0) { throw 'Preview checks failed.' }
$destination = Join-Path $PSScriptRoot '비전배리어_인게임_시연.mp4'
$arguments = @('-y','-hide_banner','-loglevel','error','-i',(Join-Path $captureFolder 'raw.mp4'))
$filters = @()
$labels = @()
$index = 1
foreach ($sound in $report.sounds) {
    $audioPath = Join-Path $projectRoot $sound.path
    if (-not (Test-Path -LiteralPath $audioPath)) { throw "Missing audio $audioPath" }
    $arguments += @('-i',$audioPath)
    $delay = [int][Math]::Round($sound.frame * 1000.0 / 30.0)
    $volume = ([double]$sound.volume * 0.6).ToString('0.###',[Globalization.CultureInfo]::InvariantCulture)
    $label = "sound$index"
    $filters += "[$index`:a]aresample=48000,volume=$volume,adelay=$delay|$delay[$label]"
    $labels += "[$label]"
    $index++
}
$filters += ($labels -join '') + "amix=inputs=$($labels.Count):normalize=0,alimiter=limit=0.85,apad,atrim=duration=10[audio]"
$arguments += @('-filter_complex',($filters -join ';'),'-map','0:v','-map','[audio]',
    '-c:v','libx264','-preset','medium','-crf','20','-pix_fmt','yuv420p','-threads','3',
    '-c:a','aac','-b:a','160k','-ac','2','-t','10','-movflags','+faststart',$destination)
& $encoderPath @arguments
if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed: $LASTEXITCODE" }
Copy-Item -LiteralPath (Join-Path $captureFolder 'report.json') -Destination (Join-Path $PSScriptRoot 'checks.json')
Copy-Item -LiteralPath (Join-Path $captureFolder 'shield-in-game.png') -Destination (Join-Path $PSScriptRoot 'shield-in-game.png')
Get-Item -LiteralPath $destination | Select-Object FullName,Length
