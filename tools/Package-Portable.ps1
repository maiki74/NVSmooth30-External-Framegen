# PowerShell · Package-Portable.ps1 · portable preview package · Windows x64 · .NET Framework 4.8
param([string]$Version='0.3.7')
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$exe=Join-Path $project 'downloads\FramegenDllTester.exe'
$binaryVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
if($binaryVersion -ne ($Version+'.0')){throw 'The binary version does not match the requested package.'}
$staging=Join-Path $project ('dist\portable-'+$Version+'-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
Copy-Item -LiteralPath $exe -Destination $staging
foreach($name in @('START-HERE.txt','README.md','LICENSE','THIRD_PARTY_NOTICES.md','CHANGELOG.md')){
    Copy-Item -LiteralPath (Join-Path $project $name) -Destination $staging
}
foreach($name in @('docs','licenses')){
    Copy-Item -LiteralPath (Join-Path $project $name) -Destination $staging -Recurse
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=Join-Path $project ('downloads\FramegenDllTester-Portable-v'+$Version+'.zip')
if(Test-Path -LiteralPath $zip){throw 'A portable ZIP already exists. Preserve it and choose another version / output before repackaging.'}
[IO.Compression.ZipFile]::CreateFromDirectory($staging,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
$checksums=@($exe,$zip) | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_) }
[IO.File]::WriteAllLines((Join-Path $project 'downloads\SHA256SUMS.txt'),$checksums,[Text.Encoding]::ASCII)
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
    $entry=$archive.GetEntry('FramegenDllTester.exe')
    if(-not $entry){throw 'The portable EXE is missing from the archive root.'}
    $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
    try{$archivedHash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose();$stream.Dispose()}
    if($archivedHash -ne (Get-FileHash -LiteralPath $exe).Hash.ToLowerInvariant()){throw 'Portable EXE verification failed.'}
    if(@($archive.Entries | Where-Object FullName -match '(^|/)(trials|private|toolchain|wizard.json)(/|$)').Count -ne 0){throw 'Private data found in portable archive.'}
}finally{$archive.Dispose()}
Write-Output ('Portable ZIP: '+$zip)
Write-Output ('EXE SHA256: '+$archivedHash)
