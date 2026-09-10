$ErrorActionPreference = 'Stop'
$probeRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
foreach ($layer in @('Infrastructure', 'Editor')) {
    $sourceDirectory = Join-Path $probeRoot "app_build/OrganicReverb/$layer"
    $targetDirectory = Join-Path $probeRoot "Assets/Shinzui/Tests/OrganicReverb/$layer"
    New-Item -ItemType Directory -Force $targetDirectory | Out-Null
    Get-ChildItem -LiteralPath $sourceDirectory -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $targetDirectory $_.Name)
    }
}
Write-Output 'Synced probe sources from app_build; existing Unity .meta files preserved.'
