# Install for the current Windows user; no administrator permission is needed.
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-p]{32}$')]
    [string] $ExtensionId,
    [string] $ExePath
)
$ErrorActionPreference = 'Stop'
# Parameter defaults are evaluated before script automatic variables are fully available.
# Resolve the portable EXE path after entering the script body.
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ExePath = Join-Path (Split-Path -Parent $scriptDirectory) 'dotSharK.exe'
}
if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) {
    throw "dotSharK.exe not found: $ExePath"
}
$exe = (Resolve-Path -LiteralPath $ExePath).ProviderPath
if ([IO.Path]::GetFileName($exe) -ne 'dotSharK.exe') {
    throw 'ExePath must point to dotSharK.exe.'
}

$root = Join-Path (Split-Path -Parent $exe) 'BrowserExtension\NativeMessaging'
[IO.Directory]::CreateDirectory($root) | Out-Null
$path = Join-Path $root 'com.rampagex.dotshark.json'
$manifest = @{
    name = 'com.rampagex.dotshark'
    description = 'dotSharK silent video download queue'
    path = $exe
    type = 'stdio'
    allowed_origins = @("chrome-extension://$ExtensionId/")
}
# JSON manifests should be UTF-8 without BOM.
$json = ConvertTo-Json -InputObject $manifest -Depth 4
[IO.File]::WriteAllText($path, $json, (New-Object System.Text.UTF8Encoding($false)))

foreach ($base in @(
    'HKCU\Software\Google\Chrome\NativeMessagingHosts',
    'HKCU\Software\Chromium\NativeMessagingHosts',
    'HKCU\Software\Microsoft\Edge\NativeMessagingHosts'
)) {
    $key = "$base\com.rampagex.dotshark"
    & reg.exe add $key /ve /t REG_SZ /d $path /f | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Registry installation failed: $key" }
}
Write-Host "dotSharK Native Messaging installed for extension ID: $ExtensionId"
Write-Host "Host EXE: $exe"
Write-Host "Manifest: $path"
Write-Host "Silent queue: $(Join-Path (Split-Path -Parent $exe) 'SilentQueue')"
