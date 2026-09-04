param(
    [string]$DotNet = 'dotnet',
    [string]$Version = '0.1.0-alpha'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$publishDirectory = Join-Path $artifacts 'win-x64'
$archive = Join-Path $artifacts "LuckyTouchInput-$Version-win-x64.zip"
$checksum = "$archive.sha256"

if (-not $publishDirectory.StartsWith($artifacts + '\',
    [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Publish directory escaped the artifacts root.'
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null

& $DotNet publish (Join-Path $root 'src\LuckyTouchInput\LuckyTouchInput.csproj') `
    -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true `
    -p:DebugType=None -o $publishDirectory
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $root 'README.zh-CN.md') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $publishDirectory

if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
if (Test-Path -LiteralPath $checksum) { Remove-Item -LiteralPath $checksum -Force }
Compress-Archive -LiteralPath @(
    (Join-Path $publishDirectory 'LuckyTouchInput.exe'),
    (Join-Path $publishDirectory 'README.md'),
    (Join-Path $publishDirectory 'README.zh-CN.md'),
    (Join-Path $publishDirectory 'LICENSE')
) `
    -DestinationPath $archive -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
[System.IO.File]::WriteAllText($checksum,
    $hash + ' *' + [System.IO.Path]::GetFileName($archive) + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))

Get-Item -LiteralPath $archive, $checksum
