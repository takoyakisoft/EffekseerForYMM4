using System.IO;

namespace EffekseerForYMM4.Tests.Interop;

public sealed class NativeAssemblyLayoutTests
{
    [Fact]
    public void NativeBootstrapperLoadsDllDirectlyFromPluginDirectory()
    {
        var root = FindRepositoryRoot();
        var sourcePath = Path.Combine(root, "EffekseerForYMM4", "Commons", "NativeAssemblyBootstrapper.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("Path.Combine(PluginDirectory, NativeLibraryFileName)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("nativepayload", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PluginCache", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EffekseerForNative.bin", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildAndPackageUseRootNativeDllLayout()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "EffekseerForYMM4", "EffekseerForYMM4.csproj"));
        var testProject = File.ReadAllText(Path.Combine(root, "EffekseerForYMM4.Tests", "EffekseerForYMM4.Tests.csproj"));
        var packageScript = File.ReadAllText(Path.Combine(root, "scripts", "package-release.ps1"));

        Assert.Contains("DestinationFiles=\"$(TargetDir)EffekseerForNative.dll\"", project, StringComparison.Ordinal);
        Assert.Contains("DestinationFiles=\"$(TargetDir)EffekseerForNative.dll\"", testProject, StringComparison.Ordinal);
        Assert.Contains("EffekseerForYMM4/EffekseerForNative.dll", packageScript, StringComparison.Ordinal);
        Assert.DoesNotContain("nativepayload/EffekseerForNative.bin", packageScript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nativepayload\\EffekseerForNative.bin", packageScript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PackageRendersVersionAndIncludesThirdPartyNotices()
    {
        var root = FindRepositoryRoot();
        var packageScript = File.ReadAllText(Path.Combine(root, "scripts", "package-release.ps1"));
        var packageReadme = File.ReadAllText(Path.Combine(root, "packaging", "Readme.txt"));
        var thirdPartyNotices = File.ReadAllText(Path.Combine(root, "packaging", "THIRD_PARTY_NOTICES.txt"));

        Assert.Contains("{VERSION}", packageReadme, StringComparison.Ordinal);
        Assert.Contains("Replace(\"{VERSION}\", $Version)", packageScript, StringComparison.Ordinal);
        Assert.Contains("THIRD_PARTY_NOTICES.txt", packageScript, StringComparison.Ordinal);
        Assert.Contains("Copyright (c) 2011 Effekseer Project", thirdPartyNotices, StringComparison.Ordinal);
        Assert.Contains("The MIT License (MIT)", thirdPartyNotices, StringComparison.Ordinal);
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
}
