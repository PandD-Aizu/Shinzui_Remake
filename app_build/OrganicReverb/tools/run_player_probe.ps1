param([int]$TimeoutSeconds = 180)
$ErrorActionPreference = 'Stop'
$probeRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$outputDirectory = Join-Path $probeRoot 'Logs/OrganicReverb/Player'
New-Item -ItemType Directory -Force $outputDirectory | Out-Null
$executable = Join-Path $probeRoot 'Builds/OrganicReverbProbe/OrganicReverbProbe.exe'
$probeArgs = '-batchmode -screen-width 960 -screen-height 540 -organicReverbOutput "' + $outputDirectory + '" -logFile "' + (Join-Path $outputDirectory 'Player.log') + '"'
$probeProcess = Start-Process -FilePath $executable -ArgumentList $probeArgs -WorkingDirectory (Split-Path $executable) -WindowStyle Hidden -PassThru
if (-not $probeProcess.WaitForExit($TimeoutSeconds * 1000)) {
    $probeProcess.Kill()
    throw 'Probe Player timed out. Inspect Logs/OrganicReverb/Player/Player.log.'
}
Write-Output "Probe Player exit code: $($probeProcess.ExitCode)"
exit $probeProcess.ExitCode
