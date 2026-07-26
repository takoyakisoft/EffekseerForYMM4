param(
    [Parameter(Mandatory = $true)]
    [string]$YMM4DirPath,
    [string]$Version = "",
    [string]$OutputDirectory = ".\artifacts"
)

$ErrorActionPreference = "Stop"

function Get-RelativePathFromBase {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,
        [Parameter(Mandatory = $true)]
        [string]$FullPath
    )

    $baseFullPath = [System.IO.Path]::GetFullPath($BasePath)
    if (-not $baseFullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $baseFullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $targetFullPath = [System.IO.Path]::GetFullPath($FullPath)
    if (-not $targetFullPath.StartsWith($baseFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is not under base path. Base=$baseFullPath Path=$targetFullPath"
    }

    return $targetFullPath.Substring($baseFullPath.Length)
}

$root = Split-Path -Parent $PSScriptRoot
$readmePath = Join-Path $root "Packaging\Readme.txt"
$licensePath = Join-Path $root "LICENSE.txt"
if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) {
    throw "Readme.txt was not found: $readmePath"
}
if (-not (Test-Path -LiteralPath $licensePath -PathType Leaf)) {
    throw "LICENSE.txt was not found: $licensePath"
}

$ymm4Dir = [System.IO.Path]::GetFullPath($YMM4DirPath)
$sourcePluginDirectory = Join-Path $ymm4Dir "user\plugin\EffekseerForYMM4"
if (-not (Test-Path -LiteralPath $sourcePluginDirectory -PathType Container)) {
    throw "Plugin output directory was not found: $sourcePluginDirectory"
}

$requiredFiles = @(
    "EffekseerForYMM4.dll",
    "EffekseerForNative.dll"
)
foreach ($relativePath in $requiredFiles) {
    $requiredPath = Join-Path $sourcePluginDirectory $relativePath
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required plugin file was not found: $requiredPath"
    }
}

$forbiddenFiles = @(
    "Ijwhost.dll",
    "EffekseerForNative.bin",
    "nativepayload",
    "native"
)
foreach ($relativePath in $forbiddenFiles) {
    $forbiddenPath = Join-Path $sourcePluginDirectory $relativePath
    if (Test-Path -LiteralPath $forbiddenPath) {
        throw "Legacy native layout/runtime file must not be packaged: $forbiddenPath"
    }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $targetsXml = [xml](Get-Content -LiteralPath (Join-Path $root "Directory.Build.targets") -Raw)
    $Version = [string]$targetsXml.Project.PropertyGroup.EffekseerForYMM4Version
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Package version could not be determined."
}

$outputDir = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
}
else {
    Join-Path $root $OutputDirectory
}
$stageRoot = Join-Path $outputDir "stage"
$packageRoot = Join-Path $stageRoot "EffekseerForYMM4"

Remove-Item -LiteralPath $stageRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

Copy-Item -LiteralPath (Join-Path $sourcePluginDirectory "EffekseerForYMM4.dll") -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $sourcePluginDirectory "EffekseerForNative.dll") -Destination $packageRoot -Force

Get-ChildItem -LiteralPath $sourcePluginDirectory -Recurse -File -Filter "EffekseerForYMM4.resources.dll" |
    ForEach-Object {
        $relativePath = Get-RelativePathFromBase -BasePath $sourcePluginDirectory -FullPath $_.FullName
        $destinationPath = Join-Path $packageRoot $relativePath
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destinationPath) | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destinationPath -Force
    }

Copy-Item -LiteralPath $readmePath -Destination (Join-Path $packageRoot "Readme.txt") -Force
Copy-Item -LiteralPath $licensePath -Destination (Join-Path $packageRoot "LICENSE.txt") -Force

Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Force |
    Where-Object { $_.Extension -in @(".pdb", ".exp", ".lib") -or $_.Name.EndsWith(".pdb.bin", [System.StringComparison]::OrdinalIgnoreCase) } |
    Remove-Item -Force

$stagedFiles = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Force)
if ($stagedFiles.Count -eq 0) {
    throw "Package stage contains no files: $packageRoot"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$baseName = "EffekseerForYMM4-v$Version"
$temporaryZipPath = Join-Path $outputDir "$baseName-plugin.zip"
$ymmeFileName = "$baseName.ymme"
$ymmePath = Join-Path $outputDir $ymmeFileName
$boothZipPath = Join-Path $outputDir "$baseName.zip"
$boothStageRoot = Join-Path $outputDir "booth-stage"

Remove-Item -LiteralPath $temporaryZipPath, $ymmePath, $boothZipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $boothStageRoot -Recurse -Force -ErrorAction SilentlyContinue

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$archive = [System.IO.Compression.ZipFile]::Open($temporaryZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $stagedFiles) {
        $entryName = Get-RelativePathFromBase -BasePath $stageRoot -FullPath $file.FullName
        $entryName = $entryName.Replace("\", "/")
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $file.FullName,
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
}
Move-Item -LiteralPath $temporaryZipPath -Destination $ymmePath -Force

$ymmeArchive = [System.IO.Compression.ZipFile]::OpenRead($ymmePath)
try {
    $requiredEntries = @(
        "EffekseerForYMM4/EffekseerForYMM4.dll",
        "EffekseerForYMM4/EffekseerForNative.dll",
        "EffekseerForYMM4/Readme.txt",
        "EffekseerForYMM4/LICENSE.txt"
    )
    foreach ($entry in $requiredEntries) {
        if (-not ($ymmeArchive.Entries | Where-Object { $_.FullName -eq $entry })) {
            throw "Required ymme entry was not found: $entry"
        }
    }
}
finally {
    $ymmeArchive.Dispose()
}

New-Item -ItemType Directory -Force -Path $boothStageRoot | Out-Null
Copy-Item -LiteralPath $ymmePath -Destination (Join-Path $boothStageRoot $ymmeFileName) -Force
Copy-Item -LiteralPath $readmePath -Destination (Join-Path $boothStageRoot "Readme.txt") -Force
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $boothStageRoot,
    $boothZipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)

$boothArchive = [System.IO.Compression.ZipFile]::OpenRead($boothZipPath)
try {
    $entries = @($boothArchive.Entries |
        Where-Object { -not [string]::IsNullOrEmpty($_.Name) } |
        ForEach-Object { $_.FullName })
    $expectedEntries = @($ymmeFileName, "Readme.txt")
    if (@($entries | Where-Object { $expectedEntries -notcontains $_ }).Count -ne 0 -or
        @($expectedEntries | Where-Object { $entries -notcontains $_ }).Count -ne 0) {
        throw "BOOTH package must contain only $ymmeFileName and Readme.txt. Actual=$($entries -join ', ')"
    }
}
finally {
    $boothArchive.Dispose()
}

Remove-Item -LiteralPath $ymmePath -Force
Remove-Item -LiteralPath $boothStageRoot -Recurse -Force

Write-Host "Created BOOTH package: $boothZipPath"
