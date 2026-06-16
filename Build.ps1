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

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Mode = 'Release',

    [ValidatePattern('^\d{4}\.\d{6}$')]
    [string] $Version,

    [switch] $DryRun,

    [switch] $Publish,

    [ValidateNotNullOrEmpty()]
    [string] $RuntimeIdentifier = 'win-x64',

    [ValidateNotNullOrEmpty()]
    [string] $Output = (Join-Path $PSScriptRoot 'Dist'),

    [switch] $NoRestore,

    [switch] $Sign,

    [string] $SigningCertificateThumbprint = $env:SCRYBE_SIGNING_CERT_THUMBPRINT,

    [string] $SigningCertificatePath = $env:SCRYBE_SIGNING_CERT_PATH,

    [string] $SigningCertificatePassword = $env:SCRYBE_SIGNING_CERT_PASSWORD,

    [string] $TimestampServer = $(if ([string]::IsNullOrWhiteSpace($env:SCRYBE_TIMESTAMP_SERVER)) { 'http://timestamp.digicert.com' } else { $env:SCRYBE_TIMESTAMP_SERVER })
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Repository = 'VBlackJack/Scrybe'

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

function Invoke-Tool {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

function Get-ProjectAssemblyName {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $ProjectPath
    )

    [xml] $projectXml = Get-Content -LiteralPath $ProjectPath
    [string] $assemblyName = $projectXml.Project.PropertyGroup |
        ForEach-Object { $_.AssemblyName } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($assemblyName)) {
        return [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
    }

    return $assemblyName
}

function Get-CurrentCommitHash {
    [string] $commit = [string] (@(& git rev-parse --short HEAD) | Select-Object -First 1)
    $commit = $commit.Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
        throw "Unable to resolve the current commit hash."
    }

    return $commit
}

function Get-VersionSequenceFromText {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Text,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $DatePrefix
    )

    [string] $escapedPrefix = [regex]::Escape($DatePrefix)
    if ($Text -match "v?$escapedPrefix(\d{2})") {
        return [int] $Matches[1]
    }

    return $null
}

function Get-GitTagSequences {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $DatePrefix
    )

    [int[]] $sequences = @()
    [string[]] $tags = @(& git tag --list "v$DatePrefix*" 2>$null)
    if ($LASTEXITCODE -ne 0) {
        return $sequences
    }

    foreach ($tag in $tags) {
        $sequence = Get-VersionSequenceFromText -Text $tag -DatePrefix $DatePrefix
        if ($null -ne $sequence) {
            $sequences += $sequence
        }
    }

    return $sequences
}

function Get-GitHubReleaseSequences {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $DatePrefix
    )

    [int[]] $sequences = @()
    if ($null -eq (Get-Command gh -ErrorAction SilentlyContinue)) {
        return $sequences
    }

    [string] $json = & gh release list --repo $Repository --json tagName --limit 100 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        return $sequences
    }

    [object[]] $releases = @($json | ConvertFrom-Json)
    foreach ($release in $releases) {
        [string] $tagName = [string] $release.tagName
        $sequence = Get-VersionSequenceFromText -Text $tagName -DatePrefix $DatePrefix
        if ($null -ne $sequence) {
            $sequences += $sequence
        }
    }

    return $sequences
}

function Get-PropsVersionSequence {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $PropsPath,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $DatePrefix
    )

    [string] $content = Get-Content -LiteralPath $PropsPath -Raw
    [string] $escapedPrefix = [regex]::Escape($DatePrefix)
    if ($content -match "<InformationalVersion>$escapedPrefix(\d{2})</InformationalVersion>") {
        return [int] $Matches[1]
    }

    return $null
}

function Resolve-BuildVersion {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $PropsPath,

        [AllowEmptyString()]
        [string] $ForcedVersion
    )

    if (-not [string]::IsNullOrWhiteSpace($ForcedVersion)) {
        if ($ForcedVersion -notmatch '^\d{4}\.\d{6}$') {
            throw "Invalid version '$ForcedVersion'. Expected YYYY.MMDDxx."
        }

        [int] $forcedSequence = [int] $ForcedVersion.Substring($ForcedVersion.Length - 2, 2)
        [string] $forcedMonthDay = $ForcedVersion.Substring(5, 4)
        return [pscustomobject] @{
            BuildNumber = $ForcedVersion
            AssemblyVersion = "1.0.$forcedMonthDay.$forcedSequence"
            Sequence = $forcedSequence
        }
    }

    [DateTime] $today = Get-Date
    [string] $datePrefix = $today.ToString('yyyy.MMdd')
    [int[]] $sequences = @()
    $sequences += Get-GitTagSequences -DatePrefix $datePrefix
    $sequences += Get-GitHubReleaseSequences -DatePrefix $datePrefix

    $propsSequence = Get-PropsVersionSequence -PropsPath $PropsPath -DatePrefix $datePrefix
    if ($null -ne $propsSequence) {
        $sequences += $propsSequence
    }

    [int] $nextSequence = 1
    if ($sequences.Count -gt 0) {
        $nextSequence = (($sequences | Sort-Object -Descending | Select-Object -First 1) + 1)
    }

    [string] $buildNumber = "{0}{1:D2}" -f $datePrefix, $nextSequence
    [string] $monthDay = $today.ToString('MMdd')

    return [pscustomobject] @{
        BuildNumber = $buildNumber
        AssemblyVersion = "1.0.$monthDay.$nextSequence"
        Sequence = $nextSequence
    }
}

function Set-BuildVersion {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $PropsPath,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $AssemblyVersion,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $InformationalVersion
    )

    [string] $content = Get-Content -LiteralPath $PropsPath -Raw
    $content = $content -replace '<Version>[^<]+</Version>', "<Version>$AssemblyVersion</Version>"
    $content = $content -replace '<InformationalVersion>[^<]+</InformationalVersion>', "<InformationalVersion>$InformationalVersion</InformationalVersion>"
    [System.IO.File]::WriteAllText($PropsPath, $content, [System.Text.UTF8Encoding]::new($false))
}

function Restore-FileContent {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Content
    )

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Assert-PublishPreconditions {
    [string] $branch = [string] (@(& git branch --show-current) | Select-Object -First 1)
    $branch = $branch.Trim()
    if ($branch -ne 'main') {
        throw "Publishing requires branch 'main'. Current branch: '$branch'."
    }

    [string[]] $status = @(& git status --porcelain)
    if ($status.Count -gt 0) {
        throw "Publishing requires a clean working tree."
    }

    [string] $remote = [string] (@(& git remote get-url origin) | Select-Object -First 1)
    $remote = $remote.Trim()
    if ($remote -notmatch 'github\.com[:/]VBlackJack/Scrybe(\.git)?$') {
        throw "Publishing requires origin to point to $Repository. Current origin: $remote"
    }

    & gh auth status --hostname github.com
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub authentication is required before publishing."
    }
}

function New-ReleaseArchive {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $PublishDirectory,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $OutputRoot,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $BuildNumber,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $RuntimeIdentifier
    )

    [string] $zipPath = Join-Path $OutputRoot "Scrybe_v${BuildNumber}_${RuntimeIdentifier}.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $PublishDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal
    return $zipPath
}

function Assert-PublishArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Published artifact missing: $Description ($Path)"
    }

    [System.IO.FileInfo] $artifact = Get-Item -LiteralPath $Path
    if ($artifact.Length -le 0) {
        throw "Published artifact is empty: $Description ($Path)"
    }
}

function Assert-PublishLayout {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $PublishDirectory,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $ExecutableFileName
    )

    [object[]] $requiredArtifacts = @(
        [pscustomobject] @{ RelativePath = $ExecutableFileName; Description = 'application executable' },
        [pscustomobject] @{ RelativePath = 'Tesseract.dll'; Description = 'Tesseract managed assembly' },
        [pscustomobject] @{ RelativePath = 'x64\leptonica-1.82.0.dll'; Description = 'Leptonica native runtime' },
        [pscustomobject] @{ RelativePath = 'x64\tesseract50.dll'; Description = 'Tesseract native runtime' },
        [pscustomobject] @{ RelativePath = 'tessdata\eng.traineddata'; Description = 'English OCR trained data' },
        [pscustomobject] @{ RelativePath = 'locales\en.json'; Description = 'English locale' },
        [pscustomobject] @{ RelativePath = 'locales\fr.json'; Description = 'French locale' }
    )

    foreach ($artifact in $requiredArtifacts) {
        Assert-PublishArtifact `
            -Path (Join-Path $PublishDirectory $artifact.RelativePath) `
            -Description $artifact.Description
    }
}

function Resolve-SigningCertificate {
    param(
        [AllowEmptyString()]
        [string] $Thumbprint,

        [AllowEmptyString()]
        [string] $CertificatePath,

        [AllowEmptyString()]
        [string] $Password
    )

    if (-not [string]::IsNullOrWhiteSpace($CertificatePath)) {
        [string] $resolvedPath = Resolve-FullPath -Path $CertificatePath
        if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
            throw "Signing certificate file not found: $resolvedPath"
        }

        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags] $flags =
            [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $certificate =
            [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($resolvedPath, $Password, $flags)

        if (-not $certificate.HasPrivateKey) {
            throw "Signing certificate '$resolvedPath' does not contain a private key."
        }

        return $certificate
    }

    if ([string]::IsNullOrWhiteSpace($Thumbprint)) {
        throw "Signing requires SCRYBE_SIGNING_CERT_THUMBPRINT or SCRYBE_SIGNING_CERT_PATH."
    }

    [string] $normalizedThumbprint = ($Thumbprint -replace '\s', '').ToUpperInvariant()
    [object[]] $matches = @()
    foreach ($storePath in @('Cert:\CurrentUser\My', 'Cert:\LocalMachine\My')) {
        if (Test-Path -LiteralPath $storePath) {
            $matches += Get-ChildItem -LiteralPath $storePath |
                Where-Object {
                    ($_.Thumbprint -replace '\s', '').ToUpperInvariant() -eq $normalizedThumbprint
                }
        }
    }

    [object] $certificate = $matches |
        Where-Object { $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($null -eq $certificate) {
        throw "No valid private signing certificate was found for thumbprint '$normalizedThumbprint'."
    }

    return $certificate
}

function Invoke-ReleaseSigning {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Path,

        [object] $Certificate,

        [AllowEmptyString()]
        [string] $TimestampServer,

        [switch] $DryRun
    )

    if ($DryRun) {
        Write-Output "Would sign: $Path"
        return
    }

    if ($null -eq $Certificate) {
        throw "Signing was requested but no certificate was resolved."
    }

    [hashtable] $signingArguments = @{
        FilePath = $Path
        Certificate = $Certificate
        HashAlgorithm = 'SHA256'
    }

    if (-not [string]::IsNullOrWhiteSpace($TimestampServer)) {
        $signingArguments.TimestampServer = $TimestampServer
    }

    [object] $signature = Set-AuthenticodeSignature @signingArguments
    if ($signature.Status -ne 'Valid') {
        throw "Authenticode signing failed for '$Path': $($signature.Status) $($signature.StatusMessage)"
    }

    Write-Output "Authenticode signature verified: $Path"
}

function New-ReleaseSbom {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $PublishDirectory,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $RepoRoot,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $OutputRoot,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $BuildNumber,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $RuntimeIdentifier
    )

    Write-Host "Restoring SBOM tool..."
    Invoke-Tool -FilePath 'dotnet' -Arguments @('tool', 'restore') | ForEach-Object { Write-Host $_ }

    [string] $componentPath = Join-Path $RepoRoot 'src'

    Write-Host "Generating SPDX SBOM..."
    Invoke-Tool -FilePath 'dotnet' -Arguments @(
        'tool',
        'run',
        'sbom-tool',
        '--',
        'generate',
        '-b',
        $PublishDirectory,
        '-bc',
        $componentPath,
        '-pn',
        'Scrybe',
        '-pv',
        $BuildNumber,
        '-ps',
        'Julien Bombled',
        '-nsb',
        'https://github.com/VBlackJack/Scrybe',
        '-nsu',
        "$BuildNumber-$RuntimeIdentifier",
        '-D',
        'true',
        '-V',
        'Warning'
    ) | ForEach-Object { Write-Host $_ }

    [string] $manifestPath = Join-Path $PublishDirectory '_manifest\spdx_2.2\manifest.spdx.json'
    Assert-PublishArtifact -Path $manifestPath -Description 'SPDX SBOM'

    [string] $releaseSbomPath = Join-Path $OutputRoot "Scrybe_v${BuildNumber}_${RuntimeIdentifier}.spdx.json"
    Copy-Item -LiteralPath $manifestPath -Destination $releaseSbomPath -Force
    Assert-PublishArtifact -Path $releaseSbomPath -Description 'release SPDX SBOM'
    return $releaseSbomPath
}

function New-ReleaseChecksumFile {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string[]] $ArtifactPaths,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $OutputRoot,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $BuildNumber,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $RuntimeIdentifier
    )

    [string] $checksumPath = Join-Path $OutputRoot "Scrybe_v${BuildNumber}_${RuntimeIdentifier}.sha256"
    [string[]] $lines = foreach ($artifactPath in $ArtifactPaths) {
        [object] $hash = Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256
        "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), [System.IO.Path]::GetFileName($artifactPath)
    }

    [System.IO.File]::WriteAllText($checksumPath, (($lines -join "`r`n") + "`r`n"), [System.Text.UTF8Encoding]::new($false))
    Assert-PublishArtifact -Path $checksumPath -Description 'SHA256 checksum file'
    return $checksumPath
}

function Publish-GitHubRelease {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $BuildNumber,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string[]] $ArtifactPaths
    )

    [string] $tag = "v$BuildNumber"
    [string[]] $assetLines = foreach ($artifactPath in $ArtifactPaths) {
        [System.IO.FileInfo] $artifact = Get-Item -LiteralPath $artifactPath
        [string] $sizeMiB = "{0:N2}" -f ($artifact.Length / 1MB)
        "- $($artifact.Name) ($sizeMiB MiB)"
    }
    [string] $notes = "Scrybe v$BuildNumber`n`nAssets:`n$($assetLines -join "`n")"

    Invoke-Tool -FilePath 'git' -Arguments @('tag', $tag)
    Invoke-Tool -FilePath 'git' -Arguments @('push', 'origin', 'main')
    Invoke-Tool -FilePath 'git' -Arguments @('push', 'origin', $tag)
    [string[]] $releaseArguments = @(
        'release',
        'create',
        $tag
    ) + $ArtifactPaths + @(
        '--repo',
        $Repository,
        '--title',
        $tag,
        '--notes',
        $notes
    )
    Invoke-Tool -FilePath 'gh' -Arguments $releaseArguments

    Write-Output "Release published: https://github.com/$Repository/releases/tag/$tag"
}

[string] $repoRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
[string] $solutionPath = Join-Path $repoRoot 'Scrybe.slnx'
[string] $projectPath = Join-Path $repoRoot 'src\Scrybe.App\Scrybe.App.csproj'
[string] $propsPath = Join-Path $repoRoot 'Directory.Build.props'
[string] $outputRoot = Resolve-FullPath -Path $Output
[string] $publishDirectory = Join-Path $outputRoot $RuntimeIdentifier
[string] $originalPropsContent = Get-Content -LiteralPath $propsPath -Raw
[object] $versionInfo = Resolve-BuildVersion -PropsPath $propsPath -ForcedVersion $Version
[string] $assemblyName = Get-ProjectAssemblyName -ProjectPath $projectPath
[string] $exePath = Join-Path $publishDirectory "$assemblyName.exe"
[string] $buildDate = ''
[string] $commitHash = ''
[string[]] $buildMetadataProperties = @()
[object] $signingCertificate = $null
[bool] $versionWasStamped = $false
[bool] $releaseWasCommitted = $false
[bool] $releaseCommitWasCreated = $false
[bool] $releaseCommitShouldRollback = $false

if ($Publish -and $Mode -ne 'Release') {
    throw "Publishing requires -Mode Release."
}

if ($Publish -and -not $DryRun) {
    Assert-PublishPreconditions
}

if ($Mode -eq 'Release') {
    $buildDate = (Get-Date).ToString('yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture)
    if (-not ($Publish -and -not $DryRun)) {
        $commitHash = Get-CurrentCommitHash
        $buildMetadataProperties = @("-p:BuildDate=$buildDate", "-p:CommitHash=$commitHash")
    }
}

Write-Output "Scrybe build"
Write-Output "Mode: $Mode"
Write-Output "Build number: $($versionInfo.BuildNumber)"
Write-Output "Assembly version: $($versionInfo.AssemblyVersion)"
if ($Mode -eq 'Release') {
    Write-Output "Build date: $buildDate"
    if (-not [string]::IsNullOrWhiteSpace($commitHash)) {
        Write-Output "Commit hash: $commitHash"
    }
}
if ($DryRun) {
    Write-Output "Dry run: true"
}
if ($Publish) {
    Write-Output "Publish: true"
}
if ($Sign) {
    Write-Output "Authenticode signing: enabled"
    Write-Output "Timestamp server: $TimestampServer"
    if (-not $DryRun) {
        $signingCertificate = Resolve-SigningCertificate `
            -Thumbprint $SigningCertificateThumbprint `
            -CertificatePath $SigningCertificatePath `
            -Password $SigningCertificatePassword
    }
}
elseif ($Publish) {
    Write-Warning "Publishing an unsigned release. Windows SmartScreen may warn users; publish the SHA256 checksum and SBOM with the release notes."
}

try {
    if ($Mode -eq 'Release') {
        Set-BuildVersion -PropsPath $propsPath -AssemblyVersion $versionInfo.AssemblyVersion -InformationalVersion $versionInfo.BuildNumber
        $versionWasStamped = $true
        Write-Output "Version stamped in Directory.Build.props."
    }

    if (-not $NoRestore) {
        Write-Output "Restoring packages in locked mode..."
        Invoke-Tool -FilePath 'dotnet' -Arguments @('restore', $solutionPath, '--locked-mode')
    }

    Write-Output "Verifying formatting..."
    Invoke-Tool -FilePath 'dotnet' -Arguments @('format', $solutionPath, '--verify-no-changes', '--no-restore')

    Write-Output "Running tests..."
    Invoke-Tool -FilePath 'dotnet' -Arguments @('test', $solutionPath, '--configuration', $Mode, '--no-restore', '--verbosity', 'normal', '-m:1')


    if ($Publish -and -not $DryRun) {
        [string] $tag = "v$($versionInfo.BuildNumber)"
        Invoke-Tool -FilePath 'git' -Arguments @('add', $propsPath)
        Invoke-Tool -FilePath 'git' -Arguments @('commit', '-m', "release: $tag")
        $releaseCommitWasCreated = $true
        $releaseCommitShouldRollback = $true

        $commitHash = Get-CurrentCommitHash
        $buildMetadataProperties = @("-p:BuildDate=$buildDate", "-p:CommitHash=$commitHash")
        Write-Output "Release commit: $commitHash"
        Write-Output "Commit hash: $commitHash"
    }

    Write-Output "Building solution..."
    [string[]] $buildArgs = @('build', $solutionPath, '--configuration', $Mode, '--no-restore') + $buildMetadataProperties
    Invoke-Tool -FilePath 'dotnet' -Arguments $buildArgs

    if (Test-Path -LiteralPath $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }

    [string[]] $publishArgs = @(
        'publish',
        $projectPath,
        '--configuration',
        $Mode,
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
        '-p:DebugSymbols=false',
        '--no-restore'
    ) + $buildMetadataProperties

    Write-Output "Publishing application..."
    Invoke-Tool -FilePath 'dotnet' -Arguments $publishArgs

    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Published executable not found: $exePath"
    }

    Assert-PublishLayout -PublishDirectory $publishDirectory -ExecutableFileName "$assemblyName.exe"
    Write-Output "Publish layout verified."

    if ($Sign) {
        Invoke-ReleaseSigning -Path $exePath -Certificate $signingCertificate -TimestampServer $TimestampServer -DryRun:$DryRun
    }

    [System.IO.FileInfo] $exe = Get-Item -LiteralPath $exePath
    Write-Output ("Published executable: {0}" -f $exe.FullName)
    Write-Output ("Size: {0:N2} MiB ({1:N0} bytes)" -f ($exe.Length / 1MB), $exe.Length)

    [string] $sbomPath = New-ReleaseSbom `
        -PublishDirectory $publishDirectory `
        -RepoRoot $repoRoot `
        -OutputRoot $outputRoot `
        -BuildNumber $versionInfo.BuildNumber `
        -RuntimeIdentifier $RuntimeIdentifier
    [System.IO.FileInfo] $sbom = Get-Item -LiteralPath $sbomPath
    Write-Output ("SBOM: {0}" -f $sbom.FullName)
    Write-Output ("SBOM size: {0:N2} MiB ({1:N0} bytes)" -f ($sbom.Length / 1MB), $sbom.Length)

    [string] $zipPath = New-ReleaseArchive -PublishDirectory $publishDirectory -OutputRoot $outputRoot -BuildNumber $versionInfo.BuildNumber -RuntimeIdentifier $RuntimeIdentifier
    [System.IO.FileInfo] $zip = Get-Item -LiteralPath $zipPath
    Write-Output ("Archive: {0}" -f $zip.FullName)
    Write-Output ("Archive size: {0:N2} MiB ({1:N0} bytes)" -f ($zip.Length / 1MB), $zip.Length)

    [string] $checksumPath = New-ReleaseChecksumFile `
        -ArtifactPaths @($zipPath, $sbomPath) `
        -OutputRoot $outputRoot `
        -BuildNumber $versionInfo.BuildNumber `
        -RuntimeIdentifier $RuntimeIdentifier
    [System.IO.FileInfo] $checksum = Get-Item -LiteralPath $checksumPath
    Write-Output ("Checksums: {0}" -f $checksum.FullName)

    if (($Publish -or $DryRun) -and $Mode -eq 'Release') {
        [string] $tag = "v$($versionInfo.BuildNumber)"
        [string[]] $releaseArtifactPaths = @($zipPath, $checksumPath, $sbomPath)
        Write-Output "Release tag: $tag"
        if ($DryRun) {
            Write-Output "Would run: git add Directory.Build.props"
            Write-Output "Would run: git commit -m `"release: $tag`""
            Write-Output "Would run: git tag $tag"
            Write-Output "Would run: git push origin main"
            Write-Output "Would run: git push origin $tag"
            [string] $quotedReleaseArtifacts = ($releaseArtifactPaths | ForEach-Object { "`"$_`"" }) -join ' '
            Write-Output "Would run: gh release create $tag $quotedReleaseArtifacts --repo $Repository --title `"$tag`" --notes <notes>"
        }
        elseif ($Publish) {
            $releaseCommitShouldRollback = $false
            Publish-GitHubRelease -BuildNumber $versionInfo.BuildNumber -ArtifactPaths $releaseArtifactPaths
            $releaseWasCommitted = $true
        }
    }
}
finally {
    if ($releaseCommitShouldRollback) {
        Invoke-Tool -FilePath 'git' -Arguments @('reset', '--mixed', 'HEAD~1')
        Restore-FileContent -Path $propsPath -Content $originalPropsContent
        Write-Output "Release commit rolled back."
    }
    elseif ($versionWasStamped -and -not $releaseWasCommitted -and -not $releaseCommitWasCreated) {
        Restore-FileContent -Path $propsPath -Content $originalPropsContent
        Write-Output "Directory.Build.props restored."
    }
}
