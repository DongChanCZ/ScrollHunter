$ErrorActionPreference = 'Stop'
$evidence = $PSScriptRoot
$priorEvidence = Join-Path (Split-Path $evidence -Parent) '2026-10-19_트레일러후속편집'
$prior = @(Get-Content -LiteralPath (Join-Path $priorEvidence 'delivery.json') -Raw | ConvertFrom-Json)
$verified = @(Get-Content -LiteralPath (Join-Path $evidence 'validated-files.json') -Raw | ConvertFrom-Json)
$backupDirectory = 'C:\Users\MBC-501-19\AppData\Local\Temp\ScrollHunter_HonestTrailer_20261019\BeforeOrbZoom'
$desktopDirectory = 'C:\Users\MBC-501-19\Desktop\발표자료'
$records = @()
foreach ($item in $verified) {
    $old = $prior | Where-Object { $_.Language -eq $item.language }
    if (-not $old -or [IO.Path]::GetDirectoryName($old.Path) -ne $desktopDirectory) { throw 'Unexpected delivery target.' }
    if ((Get-FileHash -LiteralPath $old.Path -Algorithm SHA256).Hash -ne $old.SHA256) { throw 'Desktop video changed since last delivery.' }
    if ((Get-FileHash -LiteralPath $item.source -Algorithm SHA256).Hash -ne $item.sha256) { throw 'Rendered video failed hash verification.' }
}
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
foreach ($item in $verified) {
    $old = $prior | Where-Object { $_.Language -eq $item.language }
    $backup = Join-Path $backupDirectory ([IO.Path]::GetFileName($old.Path))
    if (Test-Path -LiteralPath $backup) {
        if ((Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash -ne $old.SHA256) { throw 'Existing backup differs.' }
    } else {
        Copy-Item -LiteralPath $old.Path -Destination $backup
    }
    if ((Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash -ne $old.SHA256) { throw 'Backup verification failed.' }
    Copy-Item -LiteralPath $item.source -Destination $old.Path -Force
    if ((Get-FileHash -LiteralPath $old.Path -Algorithm SHA256).Hash -ne $item.sha256) { throw 'Delivery verification failed.' }
    $records += [ordered]@{
        Language = $item.language
        Path = $old.Path
        Backup = $backup
        Bytes = $item.bytes
        SHA256 = $item.sha256
        Duration = $item.duration
        Width = $item.width
        Height = $item.height
        FPS = $item.fps
        ZoomOutComplete = 52.1
        TheEndImpact = 53.1
    }
}
$records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidence 'delivery.json') -Encoding utf8
$records | ConvertTo-Json -Depth 4
