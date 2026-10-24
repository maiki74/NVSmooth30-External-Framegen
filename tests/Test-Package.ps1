# PowerShell · Test-Package.ps1 · disposable fixtures · Windows x64 · .NET Framework 4.8
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $project 'downloads\FramegenDllTester.exe'
$artifacts = Join-Path $project 'private\verification'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$report = Join-Path $artifacts 'self-test.txt'
$check = Start-Process -FilePath $exe -ArgumentList @('--self-test', ('"' + $report + '"')) -WindowStyle Hidden -PassThru
if (-not $check.WaitForExit(150000)) { throw "Verification timed out. Inspect PID $($check.Id); report: $report" }
if ($check.ExitCode -ne 0) { Get-Content -LiteralPath $report; throw "Self-test failed: $($check.ExitCode)" }
Get-Content -LiteralPath $report
$preview = Join-Path $artifacts 'interface.png'
$render = Start-Process -FilePath $exe -ArgumentList @('--preview', ('"' + $preview + '"')) -WindowStyle Hidden -PassThru
if (-not $render.WaitForExit(20000)) { throw "Preview did not exit: $($render.Id)" }
if ($render.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $preview)) { throw 'UI render failed.' }
Write-Output "UI render: $preview"
