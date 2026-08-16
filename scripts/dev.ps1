[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0)]
    [ValidateSet("build", "test", "fmt", "format", "lint", "check", "clean", "publish", "help")]
    [string]$Task = "help",

    [switch]$Verify,

    [switch]$ManagedOnly
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $root "Directory.Build.props"

function Show-DevHelp {
    @"
Usage:
  .\scripts\dev.ps1 <command> [options]

Commands:
  build      Build and deploy the plugin to YMM4.
  test       Run managed and Native tests.
  fmt        Format managed and Native sources. Alias: format
  format     Format managed and Native sources.
  lint       Run analyzer/lint checks.
  check      Verify formatting, run lint checks, and run tests.
  clean      Remove repository build outputs.
  publish    Build, deploy, and create the release package.
  help       Show this command list. (default)

Options:
  -Verify          With fmt/format, verify without changing files.
  -ManagedOnly     With fmt/format, process managed projects only.

Examples:
  .\scripts\dev.ps1
  .\scripts\dev.ps1 help
  .\scripts\dev.ps1 build
  .\scripts\dev.ps1 test
  .\scripts\dev.ps1 check
  .\scripts\dev.ps1 publish
"@ | Write-Host
}

if ($Task -eq "help") {
    Show-DevHelp
    return
}

function Get-BuildProperty {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if (-not (Test-Path -LiteralPath $propsPath -PathType Leaf)) {
        throw "Directory.Build.props was not found: $propsPath"
    }

    [xml]$props = Get-Content -LiteralPath $propsPath -Raw
    $node = @($props.SelectNodes("/Project/PropertyGroup/$Name")) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.InnerText) } |
        Select-Object -First 1
    if ($null -eq $node) {
        throw "$Name is not configured in Directory.Build.props."
    }

    return $node.InnerText.Trim()
}

function Get-RequiredFileProperty {
    param([Parameter(Mandatory = $true)][string]$Name)

    $path = [System.IO.Path]::GetFullPath((Get-BuildProperty $Name))
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "$Name was not found: $path"
    }

    return $path
}

function Invoke-CommandChecked {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Command
    )

    Write-Host "==> $Name" -ForegroundColor Cyan
    $global:LASTEXITCODE = 0
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

function Invoke-DotnetFormat {
    param(
        [string]$Subcommand,
        [switch]$VerifyNoChanges
    )

    $dotnet = Get-RequiredFileProperty "DotnetPath"
    $projects = @(
        (Join-Path $root "EffekseerForYMM4\EffekseerForYMM4.csproj"),
        (Join-Path $root "EffekseerForYMM4.Tests\EffekseerForYMM4.Tests.csproj")
    )
    foreach ($project in $projects) {
        $arguments = @("format")
        if (-not [string]::IsNullOrWhiteSpace($Subcommand)) {
            $arguments += $Subcommand
        }
        $arguments += @($project, "--verbosity", "minimal")
        if ($VerifyNoChanges) {
            $arguments += "--verify-no-changes"
        }
        Invoke-CommandChecked "dotnet format $Subcommand $([System.IO.Path]::GetFileName($project))" {
            & $dotnet @arguments
        }
    }
}

function Invoke-PluginBuild {
    param([switch]$Deploy)

    $skipDeploy = if ($Deploy) { "false" } else { "true" }
    Invoke-CommandChecked "Build Native and plugin$(if ($Deploy) { ', then deploy to YMM4' })" {
        & $msbuild $pluginProject /restore /t:Build /m `
            "/p:Configuration=$configuration" `
            "/p:Platform=$platform" `
            "/p:YMM4DirPath=$ymm4Dir" `
            "/p:SkipPluginDeploy=$skipDeploy"
    }
}

function Invoke-Tests {
    Invoke-CommandChecked "Build managed and Native tests" {
        & $msbuild $solution /restore /t:Build /m `
            "/p:Configuration=$configuration" `
            "/p:Platform=$platform" `
            "/p:YMM4DirPath=$ymm4Dir" `
            "/p:SkipPluginDeploy=true"
    }

    $nativeTests = Join-Path $root "EffekseerForNative.Tests\bin\$configuration\EffekseerForNative.Tests.exe"
    Invoke-CommandChecked "Run Native tests" {
        & $nativeTests
    }

    $dotnet = Get-RequiredFileProperty "DotnetPath"
    $managedTests = Join-Path $root "EffekseerForYMM4.Tests\EffekseerForYMM4.Tests.csproj"
    Invoke-CommandChecked "Run managed tests" {
        & $dotnet test $managedTests -c $configuration -p:Platform=$platform --no-build --no-restore
    }
}

function Invoke-Format {
    Invoke-DotnetFormat -VerifyNoChanges:$Verify

    if ($ManagedOnly) {
        return
    }

    $clangFormat = Get-RequiredFileProperty "ClangFormatPath"
    $nativeFiles = @(
        Get-ChildItem -LiteralPath (Join-Path $root "EffekseerForNative\src") -Recurse -File |
            Where-Object { $_.Extension -in @(".h", ".hpp", ".cpp") } |
            ForEach-Object FullName
        Get-ChildItem -LiteralPath (Join-Path $root "EffekseerForNative.Tests") -File |
            Where-Object { $_.Extension -in @(".h", ".hpp", ".cpp") } |
            ForEach-Object FullName
    )
    if ($nativeFiles.Count -gt 0) {
        Invoke-CommandChecked "$(if ($Verify) { 'Verify' } else { 'Format' }) Native sources" {
            if ($Verify) {
                & $clangFormat --dry-run --Werror @nativeFiles
            }
            else {
                & $clangFormat -i @nativeFiles
            }
        }
    }
}

function Invoke-Lint {
    Invoke-DotnetFormat "style" -VerifyNoChanges
    Invoke-DotnetFormat "analyzers" -VerifyNoChanges
}

function Remove-BuildOutputs {
    $outputPaths = @(
        "artifacts",
        "EffekseerForNative\bin",
        "EffekseerForNative\obj",
        "EffekseerForNative.Tests\bin",
        "EffekseerForNative.Tests\obj",
        "EffekseerForYMM4\bin",
        "EffekseerForYMM4\obj",
        "EffekseerForYMM4.Tests\bin",
        "EffekseerForYMM4.Tests\obj",
        "YukkuriMovieMaker.Generator\YukkuriMovieMaker.Generator\bin",
        "YukkuriMovieMaker.Generator\YukkuriMovieMaker.Generator\obj"
    )

    $rootPrefix = [System.IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
    foreach ($relativePath in $outputPaths) {
        $path = [System.IO.Path]::GetFullPath((Join-Path $root $relativePath))
        if (-not $path.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean a path outside the repository: $path"
        }
        if (Test-Path -LiteralPath $path) {
            Write-Host "Removing $relativePath"
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

function New-ReleasePackage {
    [xml]$targets = Get-Content -LiteralPath (Join-Path $root "Directory.Build.targets") -Raw
    $version = $targets.SelectSingleNode("/Project/PropertyGroup/EffekseerForYMM4Version").InnerText.Trim()
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "EffekseerForYMM4Version could not be read from Directory.Build.targets."
    }

    $sourcePluginDirectory = Join-Path $ymm4Dir "user\plugin\EffekseerForYMM4"
    $readmeTemplatePath = Join-Path $root "packaging\Readme.txt"
    $licensePath = Join-Path $root "LICENSE.txt"
    $licensesPath = Join-Path $root "LICENSES"
    $supportedCultures = @("ar-sa", "en-us", "es-es", "id-id", "ko-kr", "zh-cn", "zh-tw")
    $requiredSourceFiles = @(
        (Join-Path $sourcePluginDirectory "EffekseerForYMM4.dll"),
        (Join-Path $sourcePluginDirectory "EffekseerForNative.dll"),
        $readmeTemplatePath,
        $licensePath
    )
    $requiredSourceFiles += $supportedCultures |
        ForEach-Object { Join-Path $sourcePluginDirectory "$_\EffekseerForYMM4.resources.dll" }
    foreach ($requiredFile in $requiredSourceFiles) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "Required package file was not found: $requiredFile"
        }
    }
    if (-not (Test-Path -LiteralPath $licensesPath -PathType Container)) {
        throw "LICENSES directory was not found: $licensesPath"
    }
    $licenseFiles = @(Get-ChildItem -LiteralPath $licensesPath -File -Filter "*.txt")
    if ($licenseFiles.Count -eq 0) {
        throw "No license files were found: $licensesPath"
    }

    $outputDir = Join-Path $root "artifacts"
    $stageRoot = Join-Path $outputDir "stage"
    $packageRoot = Join-Path $stageRoot "EffekseerForYMM4"
    $boothStage = Join-Path $outputDir "booth-stage"
    $baseName = "EffekseerForYMM4-v$version"
    $ymmeFileName = "$baseName.ymme"
    $ymmePath = Join-Path $outputDir $ymmeFileName
    $zipPath = Join-Path $outputDir "$baseName.zip"

    Remove-Item -LiteralPath $stageRoot, $boothStage -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $ymmePath, $zipPath -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $packageRoot, $boothStage | Out-Null
    Copy-Item -LiteralPath (Join-Path $sourcePluginDirectory "EffekseerForYMM4.dll") -Destination $packageRoot
    Copy-Item -LiteralPath (Join-Path $sourcePluginDirectory "EffekseerForNative.dll") -Destination $packageRoot
    foreach ($culture in $supportedCultures) {
        $cultureDirectory = Join-Path $packageRoot $culture
        New-Item -ItemType Directory -Force -Path $cultureDirectory | Out-Null
        Copy-Item -LiteralPath (Join-Path $sourcePluginDirectory "$culture\EffekseerForYMM4.resources.dll") `
            -Destination $cultureDirectory
    }

    $readmeTemplate = Get-Content -LiteralPath $readmeTemplatePath -Raw
    if (-not $readmeTemplate.Contains("{VERSION}")) {
        throw "Readme.txt must contain the {VERSION} placeholder."
    }
    $packageReadmePath = Join-Path $packageRoot "Readme.txt"
    [IO.File]::WriteAllText(
        $packageReadmePath,
        $readmeTemplate.Replace("{VERSION}", $version),
        [Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath $licensePath -Destination $packageRoot
    Copy-Item -LiteralPath $licensesPath -Destination $packageRoot -Recurse

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory(
        $stageRoot, $ymmePath, [IO.Compression.CompressionLevel]::Optimal, $false)

    $archive = [IO.Compression.ZipFile]::OpenRead($ymmePath)
    try {
        $expectedEntries = @(
            "EffekseerForYMM4/EffekseerForYMM4.dll",
            "EffekseerForYMM4/EffekseerForNative.dll",
            "EffekseerForYMM4/Readme.txt",
            "EffekseerForYMM4/LICENSE.txt"
        )
        $expectedEntries += $supportedCultures |
            ForEach-Object { "EffekseerForYMM4/$_/EffekseerForYMM4.resources.dll" }
        $expectedEntries += $licenseFiles |
            ForEach-Object { "EffekseerForYMM4/LICENSES/$($_.Name)" }
        $actualEntries = @($archive.Entries | Where-Object Name | ForEach-Object FullName)
        if (@($actualEntries | Where-Object { $expectedEntries -notcontains $_ }).Count -ne 0 -or
            @($expectedEntries | Where-Object { $actualEntries -notcontains $_ }).Count -ne 0) {
            throw "YMME package entries do not match the allowlist."
        }
    }
    finally {
        $archive.Dispose()
    }

    Copy-Item -LiteralPath $ymmePath -Destination $boothStage
    Copy-Item -LiteralPath $packageReadmePath -Destination $boothStage
    [IO.Compression.ZipFile]::CreateFromDirectory(
        $boothStage, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)

    Remove-Item -LiteralPath $stageRoot, $boothStage -Recurse -Force
    Remove-Item -LiteralPath $ymmePath -Force
    Write-Host "Created release package: $zipPath" -ForegroundColor Green
}

$pluginProject = Join-Path $root "EffekseerForYMM4\EffekseerForYMM4.csproj"
$solution = Join-Path $root "EffekseerForYMM4.sln"
$configuration = "Release"
$platform = "x64"
if ($Task -ne "clean") {
    $msbuild = Get-RequiredFileProperty "MSBuildPath"
    $ymm4Dir = [System.IO.Path]::GetFullPath((Get-BuildProperty "YMM4DirPath"))
    if (-not (Test-Path -LiteralPath $ymm4Dir -PathType Container)) {
        throw "YMM4DirPath was not found: $ymm4Dir"
    }
}

switch ($Task) {
    "build" {
        Invoke-PluginBuild -Deploy
    }
    "test" {
        Invoke-Tests
    }
    "fmt" {
        Invoke-Format
    }
    "format" {
        Invoke-Format
    }
    "lint" {
        Invoke-Lint
    }
    "check" {
        Invoke-Format
        Invoke-Lint
        Invoke-Tests
    }
    "clean" {
        Remove-BuildOutputs
    }
    "publish" {
        Invoke-PluginBuild -Deploy
        New-ReleasePackage
    }
}
