using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace EffekseerForYMM4.Commons;

internal static class NativeAssemblyBootstrapper
{
    internal const string NativeLibraryName = "EffekseerForNative";
    private const string NativeLibraryFileName = $"{NativeLibraryName}.dll";
    private static readonly object SyncRoot = new();
    private static readonly string PluginDirectory =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
    private static readonly string PayloadDirectory = Path.Combine(PluginDirectory, "nativepayload");
    private static readonly string CacheRootDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YukkuriMovieMaker",
        "PluginCache",
        "EffekseerForYMM4");
    private static bool isInitialized;

    [SuppressMessage("Usage", "CA2255:ModuleInitializer 属性はライブラリ コードで使用しないでください", Justification = "The native C ABI resolver must be registered before the first P/Invoke call.")]
    [ModuleInitializer]
    internal static void Initialize() => EnsureInitialized();

    internal static void EnsureInitialized()
    {
        lock (SyncRoot)
        {
            if (isInitialized)
            {
                return;
            }

            NativeLibrary.SetDllImportResolver(typeof(NativeAssemblyBootstrapper).Assembly, ResolveNativeLibrary);
            isInitialized = true;
        }
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        _ = assembly;
        _ = searchPath;
        if (!string.Equals(libraryName, NativeLibraryName, StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        var cachePath = PrepareNativeLibrary();
        if (cachePath != null && NativeLibrary.TryLoad(cachePath, out var cacheHandle))
        {
            return cacheHandle;
        }

        var developmentPath = Path.Combine(AppContext.BaseDirectory, NativeLibraryFileName);
        return NativeLibrary.TryLoad(developmentPath, out var developmentHandle)
            ? developmentHandle
            : IntPtr.Zero;
    }

    private static string? PrepareNativeLibrary()
    {
        var sourcePath = Path.Combine(PayloadDirectory, "EffekseerForNative.bin");
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        var fingerprint = ComputeFingerprint(sourcePath);
        var cacheDirectory = Path.Combine(CacheRootDirectory, fingerprint);
        Directory.CreateDirectory(cacheDirectory);

        var destinationPath = Path.Combine(cacheDirectory, NativeLibraryFileName);
        CopyIfMissing(sourcePath, destinationPath);

        var pdbSource = Path.Combine(PayloadDirectory, "EffekseerForNative.pdb.bin");
        if (File.Exists(pdbSource))
        {
            CopyIfMissing(pdbSource, Path.Combine(cacheDirectory, "EffekseerForNative.pdb"));
        }

        return destinationPath;
    }

    private static string ComputeFingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void CopyIfMissing(string sourcePath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            return;
        }

        try
        {
            File.Copy(sourcePath, destinationPath, overwrite: false);
        }
        catch (IOException) when (File.Exists(destinationPath))
        {
        }
        catch (UnauthorizedAccessException) when (File.Exists(destinationPath))
        {
        }
    }
}
