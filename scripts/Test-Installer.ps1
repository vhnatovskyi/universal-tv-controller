param([string]$OutputDirectory = 'dist')

$ErrorActionPreference = 'Stop'
$version = (Get-Content -Raw (Join-Path $PSScriptRoot '../version.json') | ConvertFrom-Json).version
$installer = (Resolve-Path (Join-Path $OutputDirectory "BraviaRemote-Setup-$version.exe")).Path
$target = Join-Path ([IO.Path]::GetTempPath()) ("bravia-install-test-" + [guid]::NewGuid())
$app = Join-Path $target 'BraviaRemote.exe'
$uninstaller = Join-Path $target 'unins000.exe'

try {
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LANG=uk /DIR="' + $target + '"'
    $result = Start-Process -FilePath $installer -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($result.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $app -PathType Leaf)
        -or -not (Test-Path -LiteralPath (Join-Path $target 'uk/BraviaRemote.resources.dll') -PathType Leaf)) {
        throw 'Windows installation or Ukrainian language payload check failed.'
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
