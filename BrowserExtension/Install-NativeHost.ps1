# Install for the current Windows user; no administrator permission is needed.
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-p]{32}$')]
    [string] $ExtensionId,
    [string] $ExePath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'dotSharK.exe')
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $ExePath).ProviderPath
if ([IO.Path]::GetFileName($exe) -ne 'dotSharK.exe') {
    throw 'ExePath must point to dotSharK.exe.'
}

$root = Join-Path $env:LOCALAPPDATA 'dotSharK\NativeMessaging'
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
Write-Host "Silent queue/log: $env:LOCALAPPDATA\dotSharK\SilentQueue"
