param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
function Test-DotNet {
    $path = Join-Path $env:ProgramW6432 'dotnet/shared/Microsoft.NETCore.App'
    return @(Get-ChildItem $path -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^10\.0\.\d+$' }).Count -gt 0
}
function Test-WindowsRuntime {
    return @(Get-AppxPackage -Name Microsoft.WindowsAppRuntime.1.7 | Where-Object { $_.Architecture -eq 'X64' -and $_.Version -ge [version]'7000.617.2103.0' }).Count -gt 0
}
function Install-MicrosoftRuntime($url, $name, $arguments, $elevate) {
    $file = Join-Path $env:TEMP ($name + '-' + [guid]::NewGuid() + '.exe')
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $file
        $signature = Get-AuthenticodeSignature -FilePath $file
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
            throw 'The downloaded runtime does not have a valid Microsoft signature.'
        }
        $options = @{FilePath=$file; ArgumentList=$arguments; WindowStyle='Hidden'; Wait=$true; PassThru=$true}
        if ($elevate) { $options.Verb='RunAs' }
        $process = Start-Process @options
        if ($process.ExitCode -notin @(0,3010)) { throw "Runtime installation failed: $($process.ExitCode)" }
    } finally { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force } }
}
try {
    $dotnet = Test-DotNet
    $windows = Test-WindowsRuntime
    if ($CheckOnly) { if ($dotnet -and $windows) { exit 0 } else { exit 1 } }
    if (-not $dotnet) {
        Install-MicrosoftRuntime 'https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.14/dotnet-runtime-10.0.14-win-x64.exe' 'dotnet-runtime' '/install /quiet /norestart' $true
    }
    if (-not $windows) {
        Install-MicrosoftRuntime 'https://aka.ms/windowsappsdk/1.7/1.7.250909003/windowsappruntimeinstall-x64.exe' 'windows-app-runtime' '--quiet' $false
    }
    if (-not (Test-DotNet) -or -not (Test-WindowsRuntime)) { throw 'Required runtimes are not available after installation. Restart Windows and retry.' }
    exit 0
} catch {
    $_.Exception.Message | Set-Content (Join-Path $env:TEMP 'UniversalTvController-dependencies.log')
    exit 1
}
