param(
    [ValidateSet('Create', 'RunEditor', 'Build', 'RunPlayer')][string]$Action = 'Create',
    [int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
$spatialRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$logDirectory = Join-Path $spatialRoot 'Logs/SpatialAudio'
New-Item -ItemType Directory -Force $logDirectory | Out-Null
if ($Action -eq 'RunPlayer') {
    $executable = Join-Path $spatialRoot 'Builds/SpatialAudioProbe/SpatialAudioProbe.exe'
    $outputDirectory = Join-Path $logDirectory 'Player'
    New-Item -ItemType Directory -Force $outputDirectory | Out-Null
    $cliArgs = @('-batchmode', '-screen-width', '960', '-screen-height', '540', '-spatialAudioOutput', $outputDirectory, '-logFile', (Join-Path $outputDirectory 'Player.log'))
} else {
    $executable = 'C:/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe'
    $cliArgs = @('-batchmode', '-projectPath', $spatialRoot, '-executeMethod', "Shinzui.SpatialAudio.Tests.Editor.SpatialAudioProbeBuilder.$Action", '-logFile', (Join-Path $logDirectory "$Action.log"))
    if ($Action -eq 'RunEditor') { $cliArgs += @('-spatialAudioOutput', (Join-Path $logDirectory 'Editor')) }
    else { $cliArgs += '-quit' }
}
$argumentString = ($cliArgs | ForEach-Object { '"' + $_ + '"' }) -join ' '
$cliProcess = Start-Process -FilePath $executable -ArgumentList $argumentString -WorkingDirectory $spatialRoot -WindowStyle Hidden -PassThru
if (-not $cliProcess.WaitForExit($TimeoutSeconds * 1000)) {
    $cliProcess.Kill()
    throw "Spatial audio $Action exceeded $TimeoutSeconds seconds. See $logDirectory."
}
Write-Output "Spatial audio $Action exit code: $($cliProcess.ExitCode)"
exit $cliProcess.ExitCode
