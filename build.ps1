# PowerShell · build.ps1 · Framegen DLL Tester · Windows x64 · .NET Framework 4.8
param([switch]$Test)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler was not found.' }
$output = Join-Path $PSScriptRoot 'downloads'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$exe = Join-Path $output 'FramegenDllTester.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$bundle = Join-Path $PSScriptRoot 'private\bundle'
$vulkan = Join-Path $PSScriptRoot 'private\vulkan-lab\runtime'
$dxvk9 = Join-Path $PSScriptRoot 'private\dxvk\dxvk-3.1.1\x64\d3d9.dll'
if ((Get-FileHash -LiteralPath $dxvk9 -Algorithm SHA256).Hash -ne 'C126316F7478EAFF1AD8087702EDA5930F2400187B9C8B3592219C34F6632780') {throw 'The official DXVK DirectX 9 DLL does not match its recorded hash.'}
if ((Get-FileHash -LiteralPath (Join-Path $vulkan 'FramegenVulkan.dll') -Algorithm SHA256).Hash -ne '6899B3862BD7C9DFF129C803867CA3F5988AD5BBC4EEF92A54286C6EBE2E4CA8') {throw 'The compiled Vulkan layer does not match its verified build.'}
if ((Get-FileHash -LiteralPath (Join-Path $vulkan 'NVSmoothBackend.dll') -Algorithm SHA256).Hash -ne '0BFDEAD0C48C00C8732A757703D27E6788C08BC71FE817D6A7630987838AA8DC') {throw 'The prepared Vulkan backend does not match its verified build.'}
if (-not (Test-Path -LiteralPath (Join-Path $bundle 'payload.bin.gz'))) { throw 'The private bundled DLL resource is missing. Run tools/Pack-LocalDlls.py with your local source package first.' }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /utf8output "/out:$exe" "/win32manifest:$(Join-Path $PSScriptRoot 'src\app.manifest')" "/resource:$(Join-Path $bundle 'payload.bin.gz'),FramegenLab.Payload" "/resource:$(Join-Path $bundle 'manifest.json'),FramegenLab.BundleManifest" "/resource:$(Join-Path $bundle 'third-party-notices.txt'),FramegenLab.ThirdPartyNotices" "/resource:$(Join-Path $vulkan 'FramegenVulkan.dll'),FramegenLab.VulkanLayer" "/resource:$(Join-Path $vulkan 'NVSmoothBackend.dll'),FramegenLab.VulkanBackend" "/resource:$dxvk9,FramegenLab.Dxvk9" "/resource:$(Join-Path $PSScriptRoot 'licenses\DXVK-Zlib.txt'),FramegenLab.DxvkLicense" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll /reference:System.IO.Compression.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'), "$hash  FramegenDllTester.exe`r`n", [Text.Encoding]::ASCII)
Write-Output "Built: $exe"
if ($Test) { & (Join-Path $PSScriptRoot 'tests\Test-Package.ps1') }
