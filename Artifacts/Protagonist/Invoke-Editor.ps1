param([string]$Code, [string]$File, [string]$Command = 'eval', [hashtable]$Parameters)
$connection = Get-Content 'D:/Pandd/ShinShinzui/Library/Pipeline/.unity-pipeline-port' -Raw | ConvertFrom-Json
if ($File) { $Code = Get-Content -LiteralPath $File -Raw }
if (!$Parameters) { $Parameters = @{ code = $Code; timeout = 30000 } }
$body = @{ command = $Command; parameters = $Parameters; caller = 'plugin'; skill = 'unity-cli'; timeout = 30000 } | ConvertTo-Json -Depth 8
Invoke-RestMethod -Uri ('http://localhost:' + $connection.port + '/api/exec') -Method Post -Headers @{ Authorization = 'Bearer ' + $connection.evalToken } -ContentType 'application/json' -Body ([Text.Encoding]::UTF8.GetBytes($body)) | ConvertTo-Json -Depth 14
