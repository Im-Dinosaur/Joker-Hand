$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskOutput = Join-Path $taskRoot 'Temp/CoreChecks'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskDotnet = (Get-Command dotnet).Source
$taskDotnetRoot = Split-Path -Parent $taskDotnet
$taskSdk = Get-ChildItem (Join-Path $taskDotnetRoot 'sdk') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$taskRefPack = Get-ChildItem (Join-Path $taskDotnetRoot 'packs/Microsoft.NETCore.App.Ref') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$taskTfm = Get-ChildItem (Join-Path $taskRefPack.FullName 'ref') -Directory | Select-Object -First 1
$taskRuntime = Get-ChildItem (Join-Path $taskDotnetRoot 'shared/Microsoft.NETCore.App') -Directory | Where-Object { ([version]$_.Name).Major -eq ([version]$taskRefPack.Name).Major } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$taskAssembly = Join-Path $taskOutput 'CoreChecks.dll'
$taskArgs = @('/nologo', '/langversion:9', '/target:exe', '/optimize+', ('/out:"' + $taskAssembly + '"'))
$taskArgs += Get-ChildItem $taskTfm.FullName -Filter '*.dll' | ForEach-Object { '/reference:"' + $_.FullName + '"' }
$taskArgs += Get-ChildItem (Join-Path $taskRoot 'Assets/JokerHand/Core') -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
$taskArgs += '"' + (Join-Path $PSScriptRoot 'CoreHarness/Program.cs') + '"'
$taskResponse = Join-Path $taskOutput 'compile.rsp'
[System.IO.File]::WriteAllLines($taskResponse, $taskArgs)
& $taskDotnet (Join-Path $taskSdk.FullName 'Roslyn/bincore/csc.dll') ('@' + $taskResponse)
if ($LASTEXITCODE -ne 0) { throw 'Core test compilation failed.' }
$taskConfig = @{ runtimeOptions = @{ tfm = $taskTfm.Name; framework = @{ name = 'Microsoft.NETCore.App'; version = $taskRuntime.Name } } }
$taskConfig | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $taskOutput 'CoreChecks.runtimeconfig.json') -Encoding utf8
& $taskDotnet $taskAssembly
if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
