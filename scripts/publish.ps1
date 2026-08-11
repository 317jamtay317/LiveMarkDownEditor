<#
.SYNOPSIS
    Publishes Mark Down Editor, increments the installer version, and builds the installer.

.DESCRIPTION
    Runs a complete release of Mark Down Editor in the order a release has to happen:

      1. Guard   - refuses to run while the app or the Advanced Installer GUI holds the files
                   this script has to overwrite.
      2. Test    - runs the solution's test suites (skip with -SkipTests).
      3. Publish - self-contained publish of src\UI into the exact folder the installer
                   project reads its payload from. That folder is read out of the .aip, so a
                   Target Framework change in UI.csproj cannot silently point the publish
                   somewhere the installer will not look.
      4. Verify  - checks that every file the .aip references now exists on disk, and fails
                   with the list of missing files rather than letting Advanced Installer fail
                   several minutes later with a cryptic message.
      5. Version - increments ProductVersion and regenerates ProductCode. A release MUST have
                   a fresh ProductCode: reusing one with a new ProductVersion makes Windows
                   Installer reject the package with error 1638.
      6. Build   - rebuilds the installer through the Advanced Installer command line.

    The version is bumped only after a successful publish, so a failed build never leaves a
    half-released project file behind. If the installer build itself fails, the script reports
    the previous version so the .aip edit can be reverted.

    Supports -WhatIf: every step that writes reports what it would do and changes nothing.

.PARAMETER Part
    Which part of the version to increment: Major, Minor or Patch. Defaults to Patch.
    Major and Minor resets the parts to its right.

.PARAMETER Version
    An explicit three-part version (for example 2.0.0) to set instead of incrementing.

.PARAMETER KeepProductCode
    Keeps the existing ProductCode instead of generating a new one. Only correct when
    rebuilding a package that has never been released - a released version and a reused
    ProductCode make Windows Installer fail with error 1638.

.PARAMETER SkipTests
    Skips the test run.

.PARAMETER SkipPublish
    Skips the publish and reuses whatever is already in the publish folder. Useful when only
    the installer needs rebuilding.

.PARAMETER Configuration
    The build configuration to publish. Defaults to Release.

.PARAMETER Runtime
    The runtime identifier to publish. Defaults to win-x64.

.PARAMETER AdvancedInstallerPath
    Full path to AdvancedInstaller.com. Discovered automatically when omitted.

.PARAMETER BuildName
    The build inside the .aip to produce. Defaults to DefaultBuild.

.EXAMPLE
    .\scripts\publish.ps1
    Publishes, bumps 1.2.1 to 1.2.2 with a fresh ProductCode, and builds the installer.

.EXAMPLE
    .\scripts\publish.ps1 -Part Minor -SkipTests
    Publishes without running the tests and bumps 1.2.1 to 1.3.0.

.EXAMPLE
    .\scripts\publish.ps1 -Version 2.0.0 -WhatIf
    Reports every step of a 2.0.0 release without touching anything.
#>
#Requires -Version 5.1
[CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'Increment')]
param(
    [Parameter(ParameterSetName = 'Increment')]
    [ValidateSet('Major', 'Minor', 'Patch')]
    [string] $Part = 'Patch',

    [Parameter(ParameterSetName = 'Explicit', Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version,

    [switch] $KeepProductCode,
    [switch] $SkipTests,
    [switch] $SkipPublish,
    [string] $Configuration = 'Release',
    [string] $Runtime = 'win-x64',
    [string] $AdvancedInstallerPath,
    [string] $BuildName = 'DefaultBuild'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The language ID the .aip stores alongside the ProductCode ("1033:{GUID}").
$script:ProductLanguageId = 1033

function Write-Step {
    <#
    .SYNOPSIS
        Writes a numbered step header so a long release reads as a sequence, not a wall of text.
    #>
    param([Parameter(Mandatory = $true)][string] $Message)

    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Detail {
    <#
    .SYNOPSIS
        Writes an indented detail line underneath the current step.
    #>
    param([Parameter(Mandatory = $true)][string] $Message)

    Write-Host "    $Message" -ForegroundColor DarkGray
}

function Invoke-Native {
    <#
    .SYNOPSIS
        Runs a native executable and throws when it reports a non-zero exit code.
    #>
    param(
        [Parameter(Mandatory = $true)][string]   $FilePath,
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string]   $Activity
    )

    Write-Detail "$FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Activity failed with exit code $LASTEXITCODE."
    }
}

function Resolve-AdvancedInstaller {
    <#
    .SYNOPSIS
        Locates AdvancedInstaller.com, preferring an explicit path, then the newest install.
    #>
    param([string] $ExplicitPath)

    if ($ExplicitPath) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "Advanced Installer was not found at '$ExplicitPath'."
        }
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $candidates = @(
        'C:\Program Files (x86)\Caphyon\Advanced Installer*\bin\x86\AdvancedInstaller.com',
        'C:\Program Files\Caphyon\Advanced Installer*\bin\x86\AdvancedInstaller.com'
    )

    $found =
        Get-Item -Path $candidates -ErrorAction SilentlyContinue |
        Sort-Object -Property FullName -Descending |
        Select-Object -First 1

    if (-not $found) {
        throw 'AdvancedInstaller.com was not found. Pass -AdvancedInstallerPath with its full path.'
    }

    return $found.FullName
}

function Get-AipProperty {
    <#
    .SYNOPSIS
        Reads a single MSI property value out of the Advanced Installer project.
    #>
    param(
        [Parameter(Mandatory = $true)][string] $AipPath,
        [Parameter(Mandatory = $true)][string] $Name
    )

    $xml = [xml](Get-Content -LiteralPath $AipPath -Raw)
    $row = $xml.SelectSingleNode("//ROW[@Property='$Name']")
    if (-not $row) {
        throw "The installer project does not declare the '$Name' property."
    }

    return $row.GetAttribute('Value').Trim()
}

function Get-PublishDirectory {
    <#
    .SYNOPSIS
        Derives the payload folder from the installer project's own reference to UI.exe.
    .DESCRIPTION
        The .aip pins an absolute-by-convention relative path per file. Reading the folder back
        out of it - instead of rebuilding it from the Target Framework - keeps the publish and
        the installer pointing at the same place when UI.csproj retargets.
    #>
    param([Parameter(Mandatory = $true)][string] $AipPath)

    $xml = [xml](Get-Content -LiteralPath $AipPath -Raw)
    $row = $xml.SelectSingleNode("//ROW[@File='UI.exe']")
    if (-not $row) {
        throw 'The installer project has no UI.exe file entry, so the payload folder cannot be determined.'
    }

    $installerDirectory = Split-Path -Parent $AipPath
    $executablePath = [System.IO.Path]::GetFullPath((Join-Path $installerDirectory $row.GetAttribute('SourcePath')))

    return (Split-Path -Parent $executablePath)
}

function Get-MissingPayloadFile {
    <#
    .SYNOPSIS
        Returns every file the installer project references that is not on disk.
    #>
    param([Parameter(Mandatory = $true)][string] $AipPath)

    $installerDirectory = Split-Path -Parent $AipPath
    $xml = [xml](Get-Content -LiteralPath $AipPath -Raw)

    $missing = New-Object System.Collections.Generic.List[string]
    foreach ($row in $xml.SelectNodes('//ROW[@SourcePath]')) {
        $source = $row.GetAttribute('SourcePath')

        # Paths written as <AI_CUSTACTS>... resolve inside Advanced Installer itself, not here.
        if ([string]::IsNullOrWhiteSpace($source) -or $source.StartsWith('<')) { continue }

        $full = [System.IO.Path]::GetFullPath((Join-Path $installerDirectory $source))
        if (-not (Test-Path -LiteralPath $full)) {
            $missing.Add($full)
        }
    }

    # The leading comma keeps an empty result a list instead of $null.
    return ,$missing
}

function Get-NextVersion {
    <#
    .SYNOPSIS
        Increments one part of a three-part version, resetting the parts to its right.
    #>
    param(
        [Parameter(Mandatory = $true)][string] $Current,
        [Parameter(Mandatory = $true)][ValidateSet('Major', 'Minor', 'Patch')][string] $Part
    )

    $parsed = $null
    if (-not [version]::TryParse($Current, [ref] $parsed)) {
        throw "The current ProductVersion '$Current' is not a valid version."
    }

    $major = $parsed.Major
    $minor = [Math]::Max($parsed.Minor, 0)
    $patch = [Math]::Max($parsed.Build, 0)

    switch ($Part) {
        'Major' { $major++; $minor = 0; $patch = 0 }
        'Minor' { $minor++; $patch = 0 }
        'Patch' { $patch++ }
    }

    return "$major.$minor.$patch"
}

function Assert-InstallerVersion {
    <#
    .SYNOPSIS
        Rejects versions Windows Installer cannot represent.
    .DESCRIPTION
        Windows Installer reads ProductVersion as major.minor.build with major and minor capped
        at 255 and build at 65535. Anything larger is silently truncated, which breaks upgrade
        detection instead of failing loudly.
    #>
    param([Parameter(Mandatory = $true)][string] $Value)

    $parsed = [version] $Value
    if ($parsed.Major -gt 255 -or $parsed.Minor -gt 255) {
        throw "Version '$Value' is invalid: Windows Installer caps the major and minor parts at 255."
    }
    if ([Math]::Max($parsed.Build, 0) -gt 65535) {
        throw "Version '$Value' is invalid: Windows Installer caps the build part at 65535."
    }
}

function Assert-NoBlockingProcess {
    <#
    .SYNOPSIS
        Fails early when a running process holds the files the release has to overwrite.
    .DESCRIPTION
        A running Mark Down Editor locks its own publish folder, and the Advanced Installer GUI
        holds the project file - both turn into confusing mid-release failures.
    #>
    param([Parameter(Mandatory = $true)][string] $PublishDirectory)

    $editors =
        Get-Process -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Path -and $_.Path.StartsWith($PublishDirectory, [StringComparison]::OrdinalIgnoreCase)
        }

    if ($editors) {
        $names = ($editors | ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }) -join ', '
        throw "Close the running app before publishing - it locks the publish folder: $names."
    }

    $gui = Get-Process -Name 'AdvancedInstaller' -ErrorAction SilentlyContinue
    if ($gui) {
        throw 'Close the Advanced Installer GUI before publishing - it holds the .aip and overwrites edits made from the command line.'
    }
}

# --------------------------------------------------------------------------------------------
# Release
# --------------------------------------------------------------------------------------------

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$aipPath        = Join-Path $repositoryRoot 'installer\markdown_editor_installer.aip'
$solutionPath   = Join-Path $repositoryRoot 'MarkdownEditor.slnx'
$uiProjectPath  = Join-Path $repositoryRoot 'src\UI\UI.csproj'

foreach ($required in @($aipPath, $solutionPath, $uiProjectPath)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Expected to find '$required'. Run this script from inside the repository."
    }
}

$advancedInstaller = Resolve-AdvancedInstaller -ExplicitPath $AdvancedInstallerPath
$publishDirectory  = Get-PublishDirectory -AipPath $aipPath
$currentVersion    = Get-AipProperty -AipPath $aipPath -Name 'ProductVersion'
$currentCode       = Get-AipProperty -AipPath $aipPath -Name 'ProductCode'

if ($PSCmdlet.ParameterSetName -eq 'Explicit') {
    $nextVersion = $Version
} else {
    $nextVersion = Get-NextVersion -Current $currentVersion -Part $Part
}
Assert-InstallerVersion -Value $nextVersion

Write-Step 'Release plan'
Write-Detail "Repository        : $repositoryRoot"
Write-Detail "Advanced Installer: $advancedInstaller"
Write-Detail "Payload folder    : $publishDirectory"
Write-Detail "Version           : $currentVersion -> $nextVersion"
$productCodeNote = if ($KeepProductCode) { 'kept (no new release)' } else { 'regenerated' }
Write-Detail "ProductCode       : $productCodeNote"

Write-Step 'Checking for processes that would block the release'
Assert-NoBlockingProcess -PublishDirectory $publishDirectory
Write-Detail 'Nothing is holding the payload folder or the installer project.'

if ($SkipTests) {
    Write-Step 'Skipping the tests (-SkipTests)'
} elseif ($PSCmdlet.ShouldProcess($solutionPath, 'Run the test suites')) {
    Write-Step 'Running the tests'
    Invoke-Native -FilePath 'dotnet' -Activity 'The test run' -Arguments @(
        'test', $solutionPath, '-c', $Configuration, '--nologo', '-v', 'minimal'
    )
}

if ($SkipPublish) {
    Write-Step 'Skipping the publish (-SkipPublish)'
} elseif ($PSCmdlet.ShouldProcess($publishDirectory, 'Publish the app')) {
    Write-Step 'Publishing the app'
    Invoke-Native -FilePath 'dotnet' -Activity 'The publish' -Arguments @(
        'publish', $uiProjectPath,
        '-c', $Configuration,
        '-r', $Runtime,
        '--self-contained', 'true',
        '-o', $publishDirectory,
        '--nologo', '-v', 'minimal'
    )
}

Write-Step 'Verifying the installer payload'
$missing = Get-MissingPayloadFile -AipPath $aipPath
if ($missing.Count -gt 0) {
    $shown = $missing | Select-Object -First 20
    Write-Host ''
    foreach ($file in $shown) { Write-Host "    missing: $file" -ForegroundColor Red }
    if ($missing.Count -gt $shown.Count) {
        Write-Host "    ... and $($missing.Count - $shown.Count) more" -ForegroundColor Red
    }
    throw "$($missing.Count) file(s) referenced by the installer project are not on disk. Add or remove them in Advanced Installer before releasing."
}
Write-Detail 'Every file the installer project references is present.'

if ($PSCmdlet.ShouldProcess($aipPath, "Set the version to $nextVersion")) {
    Write-Step "Setting the version to $nextVersion"

    $setVersionArguments = @('/edit', $aipPath, '/SetVersion', $nextVersion)
    if ($KeepProductCode) { $setVersionArguments += '-noprodcode' }
    Invoke-Native -FilePath $advancedInstaller -Activity 'Setting the version' -Arguments $setVersionArguments

    $writtenVersion = Get-AipProperty -AipPath $aipPath -Name 'ProductVersion'
    if ($writtenVersion -ne $nextVersion) {
        throw "The installer project still reports version '$writtenVersion' after the update."
    }

    if (-not $KeepProductCode) {
        # /SetVersion is documented to regenerate the ProductCode, but a release must not depend
        # on that: an unchanged code with a changed version fails installation with error 1638.
        $writtenCode = Get-AipProperty -AipPath $aipPath -Name 'ProductCode'
        if ($writtenCode -eq $currentCode) {
            Write-Detail 'The ProductCode did not change - generating a new one explicitly.'
            Invoke-Native -FilePath $advancedInstaller -Activity 'Generating a new ProductCode' -Arguments @(
                '/edit', $aipPath, '/SetProductCode', '-langid', "$script:ProductLanguageId"
            )
            $writtenCode = Get-AipProperty -AipPath $aipPath -Name 'ProductCode'
            if ($writtenCode -eq $currentCode) {
                throw 'The ProductCode could not be regenerated. Releasing with the previous code would fail with error 1638.'
            }
        }
        Write-Detail "ProductCode: $currentCode -> $writtenCode"
    }
}

if ($PSCmdlet.ShouldProcess($aipPath, "Build the '$BuildName' installer")) {
    Write-Step "Building the installer ($BuildName)"
    try {
        Invoke-Native -FilePath $advancedInstaller -Activity 'The installer build' -Arguments @(
            '/rebuild', $aipPath, '-buildslist', $BuildName
        )
    } catch {
        Write-Host ''
        Write-Host "    The installer project was already bumped to $nextVersion." -ForegroundColor Yellow
        Write-Host "    To go back to $currentVersion : git checkout -- installer/markdown_editor_installer.aip" -ForegroundColor Yellow
        throw
    }

    $package =
        Get-ChildItem -Path (Join-Path $repositoryRoot 'installer\Setup Files') -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in @('.exe', '.msi') } |
        Sort-Object -Property LastWriteTime -Descending |
        Select-Object -First 1

    Write-Step "Released $nextVersion"
    if ($package) {
        Write-Detail "Package: $($package.FullName)"
        Write-Detail ('Size   : {0:N1} MB' -f ($package.Length / 1MB))
        Write-Detail "Built  : $($package.LastWriteTime)"
    } else {
        Write-Detail 'The build reported success but no package was found in installer\Setup Files.'
    }
    Write-Detail 'Commit the updated .aip so the released version and ProductCode are recorded.'
}
