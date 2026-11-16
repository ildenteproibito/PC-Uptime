$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$releaseExe = Join-Path $root 'artifacts\Uptime-Release\Uptime.exe'
$releaseSvg = Join-Path $root 'src\Uptime.Presentation\Assets\favicon.svg'
$installerProj = Join-Path $root 'src\Uptime.Installer\Uptime.Installer.csproj'
$payloadDir = Join-Path $root 'src\Uptime.Installer\Payload'
$outDir = Join-Path $root 'artifacts\Uptime-Installer'

if (-not (Test-Path $releaseExe)) {
    throw "Release executable not found: $releaseExe"
}

if (Test-Path $payloadDir) { Remove-Item $payloadDir -Recurse -Force }
New-Item -ItemType Directory -Path $payloadDir | Out-Null

try {
    $gzPath = Join-Path $payloadDir 'Uptime.exe.gz'
    $inStream = [System.IO.File]::OpenRead($releaseExe)
    try {
        $outStream = [System.IO.File]::Create($gzPath)
        try {
            $gzip = New-Object System.IO.Compression.GZipStream($outStream, [System.IO.Compression.CompressionLevel]::Optimal)
            try {
                $inStream.CopyTo($gzip)
            } finally {
                $gzip.Dispose()
            }
        } finally {
            $outStream.Dispose()
        }
    } finally {
        $inStream.Dispose()
    }

    if (Test-Path $releaseSvg) {
        Copy-Item $releaseSvg (Join-Path $payloadDir 'favicon.svg') -Force
    }

    if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
    dotnet publish $installerProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    Get-ChildItem -Path $outDir -Filter '*.pdb' -File | Remove-Item -Force
    Write-Host "Installer ready: $(Join-Path $outDir 'UptimeSetup.exe')"
} finally {
    if (Test-Path $payloadDir) { Remove-Item $payloadDir -Recurse -Force }
}
