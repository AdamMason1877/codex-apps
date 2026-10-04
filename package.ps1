$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$releaseVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must contain a semantic version.' }
& (Join-Path $PSScriptRoot 'build.ps1') -OutputName 'Codex Apps.package.exe'
$packageExe = Join-Path $PSScriptRoot 'Codex Apps.package.exe'
$packageTest = Start-Process -FilePath $packageExe -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath (Join-Path $PSScriptRoot 'test-results.txt')
if ($packageTest.ExitCode -ne 0) { throw 'Release tests failed.' }
$sourceFiles = @('.gitignore','.github/workflows/build.yml','app.manifest','apps.json','apps.schema.json','assets/interface.jpg','AssemblyInfo.cs','Applications.cs','Configuration.cs','DesktopIntegration.cs','Launcher.cs','MenuColors.cs','Native.cs','Organizer.cs','OrganizerTests.cs','Placement.cs','Program.cs','Tests.cs','build.ps1','package.ps1','README.md','SPEC.md','LICENSE','VERSION')
$portableFiles = @('apps.json','apps.schema.json','assets/interface.jpg','README.md','LICENSE','VERSION')
$releaseDirectory = Join-Path $PSScriptRoot 'releases'
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
function Write-ReleaseZip([string]$Destination, [string[]]$Files, [bool]$IncludeExecutable) {
    $releaseStream = [IO.File]::Open($Destination,[IO.FileMode]::Create)
    $releaseArchive = [IO.Compression.ZipArchive]::new($releaseStream,[IO.Compression.ZipArchiveMode]::Create,$false)
    try {
        foreach ($releaseFile in $Files) {
            $releaseSource = Join-Path $PSScriptRoot $releaseFile
            if (-not (Test-Path -LiteralPath $releaseSource -PathType Leaf)) { throw "Missing release file: $releaseFile" }
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($releaseArchive,$releaseSource,$releaseFile.Replace('\','/')) | Out-Null
        }
        if ($IncludeExecutable) { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($releaseArchive,$packageExe,'Codex Apps.exe') | Out-Null }
    } finally { $releaseArchive.Dispose(); $releaseStream.Dispose() }
}
$sourceZip = Join-Path $releaseDirectory "CodexApps-$releaseVersion-source.zip"
$portableZip = Join-Path $releaseDirectory "CodexApps-$releaseVersion-win-x64.zip"
Write-ReleaseZip $sourceZip $sourceFiles $false
Write-ReleaseZip $portableZip $portableFiles $true
Get-FileHash -Algorithm SHA256 -LiteralPath $sourceZip,$portableZip | ForEach-Object { '{0}  {1}' -f $_.Hash.ToLowerInvariant(),[IO.Path]::GetFileName($_.Path) } | Set-Content -LiteralPath (Join-Path $releaseDirectory 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Release archives created in $releaseDirectory"
