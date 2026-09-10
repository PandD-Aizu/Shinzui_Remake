param(
    [ValidateSet('Create', 'RunEditor', 'Build', 'RunPlayer', 'ApplyStandard', 'ApplyEconomy')][string]$Action = 'Create',
    [int]$TimeoutSeconds = 900,
    [ValidateSet("full","economy")][string]$Quality = "full"
)
$ErrorActionPreference = 'Stop'
$spatialRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$logDirectory = Join-Path $spatialRoot 'Logs/AcousticPerformance'
New-Item -ItemType Directory -Force $logDirectory | Out-Null
if ($Action -eq 'RunPlayer') {
    $executable = Join-Path $spatialRoot 'Builds/AcousticPerformanceProbe/AcousticPerformanceProbe.exe'
    $outputDirectory = Join-Path $logDirectory ('Player-' + $Quality)
    New-Item -ItemType Directory -Force $outputDirectory | Out-Null
    $cliArgs = @('-batchmode', '-screen-width', '960', '-screen-height', '540', '-acousticPerformanceOutput', $outputDirectory, '-logFile', (Join-Path $outputDirectory 'Player.log'))
} else {
    $executable = 'C:/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe'
    $cliArgs = @('-batchmode', '-projectPath', $spatialRoot, '-executeMethod', "Shinzui.AcousticPerformance.Tests.Editor.AcousticPerformanceBuilder.$Action", '-logFile', (Join-Path $logDirectory "$Action.log"))
    if ($Action -eq 'RunEditor') { $cliArgs += @('-acousticPerformanceOutput', (Join-Path $logDirectory ('Editor-' + $Quality))) }
    else { $cliArgs += '-quit' }
}
$cliArgs += @('-acousticQuality', $Quality)
$argumentString = ($cliArgs | ForEach-Object { '"' + $_ + '"' }) -join ' '
$cliProcess = Start-Process -FilePath $executable -ArgumentList $argumentString -WorkingDirectory $spatialRoot -WindowStyle Hidden -PassThru
if (-not $cliProcess.WaitForExit($TimeoutSeconds * 1000)) {
    $cliProcess.Kill()
    throw "AcousticPerformance $Action exceeded $TimeoutSeconds seconds. See $logDirectory."
}
Write-Output "AcousticPerformance $Action exit code: $($cliProcess.ExitCode)"
exit $cliProcess.ExitCode
