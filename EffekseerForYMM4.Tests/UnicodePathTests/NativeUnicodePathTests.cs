using System;
using System.Collections.Generic;
using System.IO;

namespace EffekseerForYMM4.Tests;

public sealed class NativeUnicodePathTests : IDisposable
{
    private readonly string tempRoot;

    public NativeUnicodePathTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "EffekseerForYMM4.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
    }

    [Fact]
    public void NativePathBridge_SupportsAsciiJapaneseFullWidthSpaceAndEmoji()
    {
        const string content = "ASCII|日本語|絵文字😊🚀";
        var relativePaths = new[]
        {
            "test_ascii.txt",
            "テスト.txt",
            "全角　スペース.txt",
            "emoji_😊.txt",
            Path.Combine("日本語フォルダ", "emoji_🚀.txt"),
        };

        foreach (var relativePath in relativePaths)
        {
            var combined = EffekseerForNative.NativePathBridge.Combine(tempRoot, relativePath);
            Assert.Equal(Path.GetFullPath(Path.Combine(tempRoot, relativePath)), Path.GetFullPath(combined));

            var parentDirectory = Path.GetDirectoryName(combined);
            Assert.NotNull(parentDirectory);

            Assert.True(EffekseerForNative.NativePathBridge.EnsureDirectory(parentDirectory!, out var ensureError), ensureError);
            Assert.True(EffekseerForNative.NativePathBridge.WriteUtf8TextFile(combined, content, out var writeError), writeError);
            Assert.True(EffekseerForNative.NativePathBridge.Exists(combined));

            var loaded = EffekseerForNative.NativePathBridge.ReadUtf8TextFile(combined, out var readError);
            Assert.Null(readError);
            Assert.Equal(content, loaded);
            Assert.True(File.Exists(combined));
        }
    }

    [Fact]
    public void EffekseerRenderer_LoadEffect_SupportsUnicodePaths()
    {
        var resourceRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
        Assert.True(Directory.Exists(resourceRoot), $"Resource directory not found: {resourceRoot}");

        using var renderer = new EffekseerForNative.EffekseerRenderer();
        Assert.True(renderer.Initialize(IntPtr.Zero, IntPtr.Zero, 800, 600), renderer.LastErrorMessage);

        var cases = new Dictionary<string, string>
        {
            ["ascii"] = "test_ascii.efkefc",
            ["japanese"] = "テスト.efkefc",
            ["fullwidth-space"] = "全角　スペース.efkefc",
            ["emoji"] = "emoji_😊.efkefc",
            ["nested-emoji"] = Path.Combine("日本語フォルダ", "emoji_🚀.efkefc"),
        };

        foreach (var relativeEffectPath in cases.Values)
        {
            var targetEffectPath = CopyEffectBundle(resourceRoot, relativeEffectPath);
            Assert.True(renderer.LoadEffect(targetEffectPath), renderer.LastErrorMessage ?? targetEffectPath);
        }
    }

    private string CopyEffectBundle(string sourceRoot, string relativeEffectPath)
    {
        var bundleRoot = Path.Combine(tempRoot, Path.GetDirectoryName(relativeEffectPath) ?? string.Empty);
        Directory.CreateDirectory(bundleRoot);

        CopyDirectoryRecursive(sourceRoot, bundleRoot);

        var sourceEffectPath = Path.Combine(sourceRoot, "Laser01.efkefc");
        var targetEffectPath = Path.Combine(tempRoot, relativeEffectPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetEffectPath)!);
        File.Copy(sourceEffectPath, targetEffectPath, overwrite: true);
        return targetEffectPath;
    }

    private static void CopyDirectoryRecursive(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var destination = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
