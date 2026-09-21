param([string]$Command, [string]$Parameters = '{}')
$ErrorActionPreference = 'Stop'
$descriptor = Get-Content -LiteralPath 'D:/Pandd/ShinShinzui/Library/Pipeline/.unity-pipeline-port' -Raw | ConvertFrom-Json
$body = @{ command = $Command; parameters = ($Parameters | ConvertFrom-Json); timeout = 55000 } | ConvertTo-Json -Depth 20 -Compress
Invoke-RestMethod -Uri "http://127.0.0.1:$($descriptor.port)/api/exec" -Method Post -Headers @{ Authorization = "Bearer $($descriptor.evalToken)" } -ContentType 'application/json' -Body $body -TimeoutSec 60 | ConvertTo-Json -Depth 30
