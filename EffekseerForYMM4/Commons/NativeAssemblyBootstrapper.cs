using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace EffekseerForYMM4.Commons;

internal static class NativeAssemblyBootstrapper
{
    private const string NativeAssemblyName = "EffekseerForNative";
    private static readonly string PluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
    private static readonly string PayloadDirectory = Path.Combine(PluginDirectory, "nativepayload");
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YukkuriMovieMaker",
        "PluginCache",
        "EffekseerForYMM4");
    private static readonly string NativeAssemblyPath = Path.Combine(CacheDirectory, $"{NativeAssemblyName}.dll");

    [SuppressMessage("Usage", "CA2255:ModuleInitializer 属性はライブラリ コードで使用しないでください", Justification = "YMM4 plugin load before native bridge resolution is required.")]
    [ModuleInitializer]
    internal static void Initialize()
    {
        AssemblyLoadContext.Default.Resolving += ResolveNativeAssembly;
        PrepareNativeFiles();
        TryLoadNativeAssembly();
    }

    private static Assembly? ResolveNativeAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (!string.Equals(assemblyName.Name, NativeAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return TryLoadNativeAssembly();
    }

    private static Assembly? TryLoadNativeAssembly()
    {
        if (!File.Exists(NativeAssemblyPath))
        {
            return null;
        }

        var loadedAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, NativeAssemblyName, StringComparison.OrdinalIgnoreCase));
        if (loadedAssembly != null)
        {
            return loadedAssembly;
        }

        return AssemblyLoadContext.Default.LoadFromAssemblyPath(NativeAssemblyPath);
    }

    private static void PrepareNativeFiles()
    {
        if (!Directory.Exists(PayloadDirectory))
        {
            return;
        }

        Directory.CreateDirectory(CacheDirectory);
        CopyPayload("EffekseerForNative.bin", "EffekseerForNative.dll");
        CopyPayload("Ijwhost.bin", "Ijwhost.dll");
        CopyPayload("EffekseerForNative.pdb.bin", "EffekseerForNative.pdb");
    }

    private static void CopyPayload(string sourceFileName, string destinationFileName)
    {
        var sourcePath = Path.Combine(PayloadDirectory, sourceFileName);
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var destinationPath = Path.Combine(CacheDirectory, destinationFileName);
        var shouldCopy = !File.Exists(destinationPath) || File.GetLastWriteTimeUtc(sourcePath) > File.GetLastWriteTimeUtc(destinationPath);
        if (shouldCopy)
        {
            File.Copy(sourcePath, destinationPath, true);
        }
    }
}
