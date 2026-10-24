# PowerShell · Extract-BuildInputs.ps1 · recover published embedded build inputs · Windows x64 · .NET Framework 4.8
param([string]$OutputRoot=(Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$binary=Join-Path $project 'downloads\FramegenDllTester.exe'
$expected=Get-Content -LiteralPath (Join-Path $project 'downloads\SHA256SUMS.txt') | Where-Object {$_ -match '  FramegenDllTester\.exe$'} | Select-Object -First 1
if(-not $expected -or (Get-FileHash -LiteralPath $binary).Hash.ToLowerInvariant() -ne $expected.Split(' ')[0]){throw 'Published executable SHA256 verification failed.'}
$assembly=[Reflection.Assembly]::LoadFrom($binary)
$resources=[ordered]@{
    'FramegenLab.Payload'='private\bundle\payload.bin.gz'
    'FramegenLab.BundleManifest'='private\bundle\manifest.json'
    'FramegenLab.ThirdPartyNotices'='private\bundle\third-party-notices.txt'
    'FramegenLab.VulkanLayer'='private\vulkan-lab\runtime\FramegenVulkan.dll'
    'FramegenLab.VulkanBackend'='private\vulkan-lab\runtime\NVSmoothBackend.dll'
    'FramegenLab.Dxvk9'='private\dxvk\dxvk-3.1.1\x64\d3d9.dll'
}
foreach($entry in $resources.GetEnumerator()){
    $stream=$assembly.GetManifestResourceStream($entry.Key)
    if(-not $stream){throw ('Missing build resource: '+$entry.Key)}
    $bytes=[IO.MemoryStream]::new()
    try{$stream.CopyTo($bytes);$content=$bytes.ToArray()}finally{$stream.Dispose();$bytes.Dispose()}
    $destination=Join-Path ([IO.Path]::GetFullPath($OutputRoot)) $entry.Value
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    if(Test-Path -LiteralPath $destination){
        $sha=[Security.Cryptography.SHA256]::Create()
        try{$digest=[BitConverter]::ToString($sha.ComputeHash($content)).Replace('-','')}finally{$sha.Dispose()}
        if((Get-FileHash -LiteralPath $destination).Hash -ne $digest){throw ('Existing build input differs; preserve it before extracting: '+$entry.Value)}
    }else{[IO.File]::WriteAllBytes($destination,$content)}
    Write-Output ('Verified resource: '+$entry.Value)
}
Write-Output 'Build inputs ready. The app Main method and third-party native DLLs were not executed.'
