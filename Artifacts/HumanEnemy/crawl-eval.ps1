param([string]$Code, [string]$File)
$introConn=Get-Content 'D:/PandD/Shinzui/Library/Pipeline/.unity-pipeline-port' -Raw|ConvertFrom-Json
if($File){$Code=Get-Content -LiteralPath $File -Raw}
$introBody=@{command='eval';parameters=@{code=$Code;timeout=30000};timeout=30000}|ConvertTo-Json -Depth 5
Invoke-RestMethod -Uri ('http://localhost:'+$introConn.port+'/api/exec') -Method Post -Headers @{Authorization='Bearer '+$introConn.evalToken} -ContentType 'application/json' -Body ([Text.Encoding]::UTF8.GetBytes($introBody)) | ConvertTo-Json -Depth 12
