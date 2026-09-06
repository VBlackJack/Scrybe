#Requires -Version 5.1
<#
Copyright 2026 Julien Bombled
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0
Unless required by applicable law or agreed to in writing, software distributed
under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
CONDITIONS OF ANY KIND, either express or implied. See the License for the
specific language governing permissions and limitations under the License.
#>
<#
.SYNOPSIS
Creates an offline Windows Sandbox self-test configuration for a published Scrybe package.
.DESCRIPTION
Maps the package read-only and a new evidence directory read-write. Does not install
Windows Sandbox or launch it. Open the emitted WSB file on a Sandbox-capable host.
.PARAMETER PublishDirectory
Existing self-contained Windows x64 publish directory.
.PARAMETER OutputDirectory
New directory for the WSB file, bootstrap, reports and daily log.
.PARAMETER TimeoutSeconds
Maximum time the packaged self-test may run inside the sandbox.
.EXAMPLE
./tools/New-PackageSandbox.ps1 -PublishDirectory ./artifacts/publish -OutputDirectory ./artifacts/sandbox-check
.NOTES
Exit codes: 0 configuration created (or WhatIf), 2 invalid inputs, 3 write failure.
Configuration syntax: https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $PublishDirectory,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string] $OutputDirectory,
    [Parameter()][ValidateRange(10, 300)][int] $TimeoutSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
try {
    [string] $package = (Resolve-Path -LiteralPath $PublishDirectory).Path
    [string] $evidence = [System.IO.Path]::GetFullPath($OutputDirectory)
    if (-not (Test-Path -LiteralPath (Join-Path $package 'Scrybe.exe') -PathType Leaf)) { exit 2 }
    if (Test-Path -LiteralPath $evidence) { Write-Error 'Use a new evidence directory.' -ErrorAction Continue; exit 2 }
    if (-not $PSCmdlet.ShouldProcess($evidence, 'Create offline sandbox verification files')) { exit 0 }
    New-Item -ItemType Directory -Path $evidence | Out-Null
    [string] $bootstrap = @'
#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
try {
    $process = Start-Process -FilePath 'C:\Package\Scrybe.exe' -ArgumentList @('--self-test', 'C:\Evidence\result.json') -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(__TIMEOUT_MS__)) {
        $process.Kill()
        throw 'Packaged self-test timed out.'
    }
    if ($process.ExitCode -ne 0) { throw "Packaged self-test exited $($process.ExitCode)." }
    $result = Get-Content -LiteralPath 'C:\Evidence\result.json' -Raw | ConvertFrom-Json
    if (-not $result.passed) { throw 'Packaged self-test reported failure.' }
    [System.IO.File]::WriteAllText('C:\Evidence\sandbox-receipt.json', '{"passed":true,"environment":"Windows Sandbox"}')
    exit 0
} catch {
    @{ passed = $false; environment = 'Windows Sandbox'; error = $_.Exception.Message } |
        ConvertTo-Json | Set-Content -LiteralPath 'C:\Evidence\sandbox-receipt.json' -Encoding UTF8
    exit 1
}
'@
    $bootstrap = $bootstrap.Replace('__TIMEOUT_MS__', [string]($TimeoutSeconds * 1000))
    [string] $packageXml = [System.Security.SecurityElement]::Escape($package)
    [string] $evidenceXml = [System.Security.SecurityElement]::Escape($evidence)
    [string] $configuration = @"
<Configuration>
  <VGpu>Disable</VGpu>
  <Networking>Disable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <MappedFolders>
    <MappedFolder><HostFolder>$packageXml</HostFolder><SandboxFolder>C:\Package</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$evidenceXml</HostFolder><SandboxFolder>C:\Evidence</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Evidence\Run-SelfTest.ps1</Command></LogonCommand>
</Configuration>
"@
    [System.Text.UTF8Encoding] $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Join-Path $evidence 'Run-SelfTest.ps1'), $bootstrap, $encoding)
    [System.IO.File]::WriteAllText((Join-Path $evidence 'Scrybe.wsb'), $configuration, $encoding)
    [string] $log = '[{0}] [INFO] Sandbox configuration created for {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $package
    Add-Content -LiteralPath (Join-Path $evidence ('Sandbox_{0}.log' -f (Get-Date -Format 'yyyyMMdd'))) -Value $log -Encoding UTF8
    Write-Output (Join-Path $evidence 'Scrybe.wsb')
    exit 0
} catch {
    Write-Error "Could not prepare sandbox check: $($_.Exception.Message)" -ErrorAction Continue
    exit 3
}
