[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$publishPath = [System.IO.Path]::GetFullPath($PublishDirectory)
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $publishPath -PathType Container)) { throw "Publish directory does not exist: $publishPath" }
if ($publishPath.TrimEnd('\') -eq $outputPath.TrimEnd('\')) { throw 'Obfuscator output must be separate from the publish directory.' }
$assemblies = @('Uptime.dll', 'Uptime.Core.dll', 'Uptime.Infrastructure.dll')
foreach ($assembly in $assemblies) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishPath $assembly) -PathType Leaf)) { throw "Required application assembly not found: $assembly" }
}
if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Recurse -Force }
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
function ConvertTo-XmlAttributeValue([string]$Value) { return [System.Security.SecurityElement]::Escape($Value) }
$inPathXml = ConvertTo-XmlAttributeValue $publishPath
$outPathXml = ConvertTo-XmlAttributeValue $outputPath
$moduleLines = foreach ($assembly in $assemblies) {
    $modulePath = ConvertTo-XmlAttributeValue (Join-Path $publishPath $assembly)
    '  <Module file="{0}" />' -f $modulePath
}
$configPath = Join-Path ([System.IO.Path]::GetTempPath()) ("uptime-obfuscar-{0}.xml" -f [guid]::NewGuid())
$config = @"
<?xml version="1.0" encoding="utf-8"?>
<Obfuscator>
  <Var name="InPath" value="$inPathXml" />
  <Var name="OutPath" value="$outPathXml" />
  <Var name="KeepPublicApi" value="true" />
  <Var name="HidePrivateApi" value="true" />
  <Var name="HideStrings" value="false" />
  <Var name="SkipGenerated" value="true" />
  <Var name="SkipSpecialName" value="true" />
  <Var name="ReuseNames" value="false" />
$($moduleLines -join [Environment]::NewLine)
</Obfuscator>
"@
try {
    [System.IO.File]::WriteAllText($configPath, $config, [System.Text.UTF8Encoding]::new($false))
    Write-Host 'Obfuscating application-owned assemblies only...'
    & dotnet tool run obfuscar.console -- $configPath
    if ($LASTEXITCODE -ne 0) { throw "Obfuscar failed with exit code $LASTEXITCODE." }
    foreach ($assembly in $assemblies) {
        $obfuscatedAssembly = Join-Path $outputPath $assembly
        if (-not (Test-Path -LiteralPath $obfuscatedAssembly -PathType Leaf)) { throw "Obfuscar did not produce the expected assembly: $assembly" }
        Copy-Item -LiteralPath $obfuscatedAssembly -Destination (Join-Path $publishPath $assembly) -Force
    }
    Get-ChildItem -LiteralPath $outputPath -File -Filter 'Mapping.*' -ErrorAction SilentlyContinue | Remove-Item -Force
    Get-ChildItem -LiteralPath $publishPath -Recurse -File -Filter '*.pdb' | Remove-Item -Force
    Write-Host 'Obfuscation completed. Only Uptime-owned assemblies were rewritten.'
}
finally {
    Remove-Item -LiteralPath $configPath -Force -ErrorAction SilentlyContinue
}
