param([string]$OutputDirectory = 'dist', [switch]$BootstrapOnly)

$ErrorActionPreference = 'Stop'
$version = (Get-Content -Raw (Join-Path $PSScriptRoot '../version.json') | ConvertFrom-Json).version
$installer = (Resolve-Path (Join-Path $OutputDirectory "UniversalTvController-Setup-$version.exe")).Path
$target = Join-Path ([IO.Path]::GetTempPath()) ("bravia-install-test-" + [guid]::NewGuid())
$app = Join-Path $target 'UniversalTvController.exe'
$uninstaller = Join-Path $target 'unins000.exe'

try {
    $log = Join-Path ([IO.Path]::GetTempPath()) 'UniversalTvController-installer-test.log'
    $arguments = '/LOG="' + $log + '" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LANG=uk /DIR="' + $target + '"'
    $result = Start-Process -FilePath $installer -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    $metadataFiles = if ($BootstrapOnly) { @($installer) } else { @($installer, $app, (Join-Path $target 'UniversalTvController.dll')) }
    foreach ($file in $metadataFiles) {
        if (Test-Path -LiteralPath $file) {
            $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($file)
            if ($info.CompanyName.Trim() -ne 'Vladyslav Hnatovskyi' -or $info.LegalCopyright -notlike '*vhnatovskyi@hotmail.com*') {
                throw "Developer metadata missing: $file"
            }
        }
    }
    $localizedAssembly = Join-Path $target 'uk/UniversalTvController.resources.dll'
    if ($result.ExitCode -ne 0) {
        Get-Content -LiteralPath $log -Tail 80 -ErrorAction SilentlyContinue | Write-Host
        Get-Content -LiteralPath (Join-Path ([IO.Path]::GetTempPath()) 'UniversalTvController-dependencies.log') -ErrorAction SilentlyContinue | Write-Host
        throw "Windows installer exited with code $($result.ExitCode)."
    }
    if (-not (Test-Path -LiteralPath $app -PathType Leaf) -or -not (Test-Path -LiteralPath $localizedAssembly -PathType Leaf)) {
        throw 'Windows installation is missing the executable or the Ukrainian language payload.'
    }
}
finally {
    if (Test-Path -LiteralPath $uninstaller -PathType Leaf) {
        $result = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -Wait -PassThru
        if ($result.ExitCode -ne 0 -or (Test-Path -LiteralPath $app)) {
            throw 'Windows uninstallation check failed.'
        }
    }
}
