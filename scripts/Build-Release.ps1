param(
    [string]$OutputDirectory = 'dist',
    [string]$Tag,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/App/BraviaRemote.csproj'
$version = (Get-Content -Raw -LiteralPath (Join-Path $root 'version.json') | ConvertFrom-Json).version
if ($version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
    throw 'version.json must contain a three-part numeric version.'
}
$projectXml = [xml](Get-Content -Raw -LiteralPath $project)
$appVersion = $projectXml.SelectSingleNode('/Project/PropertyGroup/ApplicationDisplayVersion').InnerText
if ($appVersion -cne $version -or ($Tag -and $Tag -cne "v$version")) {
    throw 'The project version, version.json, and release tag must match.'
}

$outputPath = (New-Item -ItemType Directory -Path $OutputDirectory -Force).FullName
$stage = Join-Path ([IO.Path]::GetTempPath()) ("bravia-release-" + [guid]::NewGuid())
$archive = Join-Path $outputPath "BraviaRemote-$version-Windows-x64.zip"
$setup = Join-Path $outputPath "BraviaRemote-Setup-$version.exe"
if ((Test-Path -LiteralPath $archive) -or (Test-Path -LiteralPath $setup)) {
    throw "Release files for $version already exist in $outputPath"
}
if (-not $SkipInstaller) {
    $compiler = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
    if (-not $compiler) {
        $compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
    }
    if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
        throw 'Inno Setup 6 is required to build the installer.'
    }
}

try {
    $target = 'net10.0-windows10.0.19041.0'
    dotnet publish $project -f $target -p:BraviaWindowsOnly=true -c Release -r win-x64 --self-contained -o $stage
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
    if (-not (Test-Path -LiteralPath (Join-Path $stage 'BraviaRemote.exe') -PathType Leaf)) {
        throw 'Windows executable is missing.'
    }
    if (-not $SkipInstaller) {
        & $compiler "/DAppVersion=$version" "/DPayloadDirectory=$stage" "/DOutputDirectory=$outputPath" (Join-Path $root 'installer/BraviaRemote.iss')
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $setup -PathType Leaf)) {
            throw 'Windows installer compilation failed.'
        }
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $archive, [IO.Compression.CompressionLevel]::Optimal, $false)
    $files = if ($SkipInstaller) { @($archive) } else { @($archive, $setup) }
    $files | ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $([IO.Path]::GetFileName($_))"
    } | Set-Content -LiteralPath (Join-Path $outputPath 'SHA256SUMS.txt') -Encoding ascii
}
finally {
    if (Test-Path -LiteralPath $stage) {
        $resolvedStage = (Resolve-Path -LiteralPath $stage).Path
        $separator = [IO.Path]::DirectorySeparatorChar
        $resolvedTemp = (Resolve-Path -LiteralPath ([IO.Path]::GetTempPath())).Path.TrimEnd($separator) + $separator
        if ($resolvedStage.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolvedStage -Recurse -Force
        }
    }
}
