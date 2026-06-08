# Copyright 2026 Julien Bombled
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [ValidateNotNullOrEmpty()]
    [string] $RuntimeIdentifier = 'win-x64',

    [ValidateNotNullOrEmpty()]
    [string] $Output = (Join-Path $PSScriptRoot 'Dist'),

    [switch] $NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-FullPath {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Path))
}

$repoRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$projectPath = Join-Path $repoRoot 'src\Scrybe.App\Scrybe.App.csproj'
$outputRoot = Resolve-FullPath -Path $Output
$publishDirectory = Join-Path $outputRoot $RuntimeIdentifier

if (-not (Test-Path -LiteralPath $projectPath)) {
    Write-Error "Project file not found: $projectPath"
}

[xml] $projectXml = Get-Content -LiteralPath $projectPath
$assemblyName = $projectXml.Project.PropertyGroup |
    ForEach-Object { $_.AssemblyName } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($assemblyName)) {
    $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
}

$exePath = Join-Path $publishDirectory "$assemblyName.exe"

if (Test-Path -LiteralPath $publishDirectory) {
    if ($PSCmdlet.ShouldProcess($publishDirectory, 'Remove existing publish directory')) {
        Write-Verbose "Removing existing publish directory: $publishDirectory"
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }
}

$publishArgs = @(
    'publish',
    $projectPath,
    '--configuration',
    $Configuration,
    '--runtime',
    $RuntimeIdentifier,
    '--self-contained',
    'true',
    '--output',
    $publishDirectory,
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:PublishTrimmed=false',
    '-p:DebugType=embedded',
    '-p:DebugSymbols=false'
)

if ($NoRestore) {
    $publishArgs += '--no-restore'
}

if (-not $PSCmdlet.ShouldProcess($projectPath, "Publish $Configuration $RuntimeIdentifier to $publishDirectory")) {
    Write-Output "Publish skipped. Expected executable path: $exePath"
    return
}

Write-Verbose "Publishing: dotnet $($publishArgs -join ' ')"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $exePath)) {
    Write-Error "Published executable not found: $exePath"
}

$exe = Get-Item -LiteralPath $exePath
$sizeMiB = $exe.Length / 1MB
Write-Output ("Published executable: {0}" -f $exe.FullName)
Write-Output ("Size: {0:N2} MiB ({1:N0} bytes)" -f $sizeMiB, $exe.Length)
