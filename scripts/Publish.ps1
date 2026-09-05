param(
    [string]$DotNet = 'dotnet',
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Version) {
    $project = [xml](Get-Content -LiteralPath (Join-Path $root 'src/LuckyTouchInput/LuckyTouchInput.csproj') -Raw)
    $Version = [string]$project.Project.PropertyGroup.Version
    if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Invalid project version.' }
}
$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts ('.publish-' + [guid]::NewGuid().ToString('N'))
$publishDirectory = Join-Path $artifacts 'win-x64'
$archive = Join-Path $artifacts "LuckyTouchInput-$Version-win-x64.zip"
$temporaryArchive = "$stage.zip"
Push-Location $root
try {
    # Validate the toolchain before touching any previous publish output.
    $sdkVersion = & $DotNet --version
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^10\.') {
        throw 'A .NET 10 SDK is required. Pass -DotNet with its executable path if needed.'
    }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    & $DotNet publish src/LuckyTouchInput/LuckyTouchInput.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None "-p:Version=$Version" -o $stage
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed; previous release files were preserved.' }
    foreach ($name in @('README.md', 'README.zh-CN.md', 'LICENSE', 'SECURITY.md', 'CONTRIBUTING.md', 'CHANGELOG.md', 'docs', 'assets')) {
        Copy-Item -LiteralPath (Join-Path $root $name) -Destination $stage -Recurse
    }
    $assets = Get-Content 'src/LuckyTouchInput/obj/project.assets.json' -Raw | ConvertFrom-Json
    $deps = Get-Content 'src/LuckyTouchInput/bin/Release/net10.0-windows/win-x64/LuckyTouchInput.deps.json' -Raw | ConvertFrom-Json
    foreach ($runtime in @($deps.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'runtimepack.*' })) {
        $package = $runtime.Substring('runtimepack.'.Length).ToLowerInvariant()
        $packagePath = $null
        foreach ($cache in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path $cache $package
            if (Test-Path -LiteralPath $candidate) { $packagePath = $candidate; break }
        }
        if (-not $packagePath) { throw "Runtime package/license missing: $package" }
        $licenseDestination = Join-Path $stage ('licenses/' + $package.Replace('/', '-'))
        New-Item -ItemType Directory -Path $licenseDestination -Force | Out-Null
        foreach ($notice in Get-ChildItem -LiteralPath $packagePath -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)(\..*)?$' }) {
            Copy-Item -LiteralPath $notice.FullName -Destination $licenseDestination
        }
        if (-not (Get-ChildItem -LiteralPath $licenseDestination -File)) { throw "Runtime license not found: $package" }
    }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $temporaryArchive -CompressionLevel Optimal
    & (Join-Path $PSScriptRoot 'Test-Package.ps1') -Archive $temporaryArchive
    $hash = (Get-FileHash -LiteralPath $temporaryArchive -Algorithm SHA256).Hash
    Move-Item -LiteralPath $temporaryArchive -Destination $archive -Force
    [IO.File]::WriteAllText("$archive.sha256", "$hash  $([IO.Path]::GetFileName($archive))" + [Environment]::NewLine)
    if (-not [IO.Path]::GetFullPath($publishDirectory).StartsWith($artifacts + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Publish directory escaped artifacts.'
    }
    if (Test-Path -LiteralPath $publishDirectory) { Remove-Item -LiteralPath $publishDirectory -Recurse -Force }
    Move-Item -LiteralPath $stage -Destination $publishDirectory
    Write-Output "Release: $archive"
    Write-Output "SHA256: $hash"
}
finally {
    if (Test-Path -LiteralPath $temporaryArchive) { Remove-Item -LiteralPath $temporaryArchive -Force }
    if ([IO.Path]::GetFullPath($stage).StartsWith($artifacts + '\.publish-', [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $stage)) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
    Pop-Location
}
