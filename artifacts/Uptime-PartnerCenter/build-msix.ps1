param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9.-]{3,50}$')]
    [string]$IdentityName,

    [Parameter(Mandatory = $true)]
    [string]$Publisher,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[1-9][0-9]{0,4}\.[0-9]{1,5}\.[0-9]{1,5}\.0$')]
    [string]$Version,

    [string]$PublisherDisplayName = 'Uptime'
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$release = Join-Path $root 'artifacts\Uptime-Release'
$template = Join-Path $PSScriptRoot 'AppxManifest.template.xml'
$logoSource = Join-Path $PSScriptRoot 'Assets'
$layout = Join-Path $PSScriptRoot "PackageLayout-$Version"
$package = Join-Path $PSScriptRoot "Uptime-$Version-x64.msix"

if (-not (Test-Path (Join-Path $release 'Uptime.exe'))) { throw "Release payload not found: $release" }
if ($Publisher -notmatch '^CN=') { throw 'Publisher must be copied exactly from Partner Center and normally starts with CN=.' }

$makeAppxCandidates = @(
    (Join-Path $env:ProgramFiles 'Windows Kits\10\bin\10.0.26100.0\x64\MakeAppx.exe'),
    'C:\Users\Admin\Downloads\PC_Uptime_Source\build_tools\bin\10.0.26100.0\x64\makeappx.exe'
)
$makeAppx = $makeAppxCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $makeAppx) { throw 'MakeAppx.exe not found. Install the Windows 10/11 SDK, then rerun this script.' }

New-Item -ItemType Directory -Path $layout -Force | Out-Null
Copy-Item (Join-Path $release '*') -Destination $layout -Recurse -Force
Get-ChildItem -LiteralPath $layout -Filter '*.pdb' -Recurse -File | Remove-Item -Force
Copy-Item (Join-Path $logoSource '*.png') -Destination (Join-Path $layout 'Assets') -Force

$manifest = Get-Content -LiteralPath $template -Raw
$manifest = $manifest.Replace('__IDENTITY_NAME__', [System.Security.SecurityElement]::Escape($IdentityName))
$manifest = $manifest.Replace('__PUBLISHER__', [System.Security.SecurityElement]::Escape($Publisher))
$manifest = $manifest.Replace('__VERSION__', [System.Security.SecurityElement]::Escape($Version))
$manifest = $manifest.Replace('__PUBLISHER_DISPLAY_NAME__', [System.Security.SecurityElement]::Escape($PublisherDisplayName))
[System.IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, [System.Text.UTF8Encoding]::new($false))

& $makeAppx pack /v /o /d $layout /p $package
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed with exit code $LASTEXITCODE." }
Write-Host "Partner Center package created: $package"
