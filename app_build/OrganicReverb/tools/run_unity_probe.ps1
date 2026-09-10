param(
    [ValidateSet('Create', 'RunEditor', 'Build')][string]$Action = 'Create',
    [int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
$probeRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$unityPath = 'C:/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe'
$logDirectory = Join-Path $probeRoot 'Logs/OrganicReverb'
New-Item -ItemType Directory -Force $logDirectory | Out-Null
$probeArgs = @('-batchmode', '-projectPath', $probeRoot, '-executeMethod', "Shinzui.AudioProbe.Editor.ProbeBuilder.$Action", '-logFile', (Join-Path $logDirectory "$Action.log"))
if ($Action -ne 'RunEditor') { $probeArgs += '-quit' }
else { $probeArgs += @('-organicReverbOutput', (Join-Path $probeRoot 'Logs/OrganicReverb/Editor')) }
# Quote each literal argument for Windows CreateProcess; this script never invokes another shell.
$argumentString = ($probeArgs | ForEach-Object { '"' + $_ + '"' }) -join ' '
$probeProcess = Start-Process -FilePath $unityPath -ArgumentList $argumentString -WorkingDirectory $probeRoot -WindowStyle Hidden -PassThru
# Wait only for Unity, not for its long-lived licensing/compilation service descendants.
if (-not $probeProcess.WaitForExit($TimeoutSeconds * 1000)) {
    $probeProcess.Kill()
    throw "Unity CLI $Action exceeded $TimeoutSeconds seconds. See $logDirectory/$Action.log"
}
Write-Output "Unity CLI $Action exit code: $($probeProcess.ExitCode)"
exit $probeProcess.ExitCode
