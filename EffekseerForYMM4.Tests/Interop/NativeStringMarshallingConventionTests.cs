using System.IO;
using System.Text.RegularExpressions;

namespace EffekseerForYMM4.Tests;

public sealed partial class NativeStringMarshallingConventionTests
{
    [Fact]
    public void ManagedNativeInteropUsesUtf8StringMarshallingOnly()
    {
        var root = FindRepositoryRoot();
        var sourcePath = Path.Combine(root, "EffekseerForYMM4", "Commons", "NativeMethods.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("StringMarshalling.Utf16", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UnmanagedType.LPWStr", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UnmanagedType.LPUTF8Str", source, StringComparison.Ordinal);
        Assert.Matches(
            Utf8LoadEffectImportPattern(),
            source);
    }

    [Fact]
    public void NativeExportHeaderExposesUtf8StringsOnly()
    {
        var root = FindRepositoryRoot();
        var headerPath = Path.Combine(
            root,
            "EffekseerForNative",
            "src",
            "Wrapper",
            "EffekseerRenderer.h");
        var source = File.ReadAllText(headerPath);

        Assert.DoesNotContain("wchar_t", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_wide", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(WideEntryPointPattern(), source);
        Assert.Matches(Utf8NativePathParameterPattern(), source);
    }

    [Fact]
    public void NativePathBoundaryUsesStrictUtf8AndWideWin32Paths()
    {
        var root = FindRepositoryRoot();
        var helperPath = Path.Combine(
            root,
            "EffekseerForNative",
            "src",
            "Core",
            "WindowsString.h");
        var wrapperPath = Path.Combine(
            root,
            "EffekseerForNative",
            "src",
            "Wrapper",
            "EffekseerRenderer.cpp");
        var fileIoPath = Path.Combine(
            root,
            "EffekseerForNative",
            "vendor",
            "effekseer",
            "src",
            "Effekseer",
            "Effekseer",
            "Effekseer.DefaultFile.cpp");
        var helper = File.ReadAllText(helperPath);
        var wrapper = File.ReadAllText(wrapperPath);
        var fileIo = File.ReadAllText(fileIoPath);

        Assert.Contains("CP_UTF8", helper, StringComparison.Ordinal);
        Assert.Contains("MB_ERR_INVALID_CHARS", helper, StringComparison.Ordinal);
        Assert.Contains("GetFullPathNameW", helper, StringComparison.Ordinal);
        Assert.Contains("L\"\\\\\\\\?\\\\UNC\\\\\"", helper, StringComparison.Ordinal);
        Assert.Contains("Utf8ToAbsolutePath(pathUtf8)", wrapper, StringComparison.Ordinal);
        Assert.Contains("ToWin32ApiPath(", fileIo, StringComparison.Ordinal);
        Assert.Contains("_wfopen_s", fileIo, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EffekseerForYMM4.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    [GeneratedRegex(
        @"LibraryImport\([^\)]*effekseer_renderer_load_effect[^\)]*StringMarshalling\s*=\s*StringMarshalling\.Utf8[^\)]*\)",
        RegexOptions.Singleline)]
    private static partial Regex Utf8LoadEffectImportPattern();

    [GeneratedRegex(
        @"effekseer_renderer_load_effect\s*\([^\)]*\bconst\s+char\s*\*\s*pathUtf8\b[^\)]*\)",
        RegexOptions.Singleline)]
    private static partial Regex Utf8NativePathParameterPattern();

    [GeneratedRegex(@"effekseer_[A-Za-z0-9_]*(?:_w|W)\s*\(")]
    private static partial Regex WideEntryPointPattern();
}
