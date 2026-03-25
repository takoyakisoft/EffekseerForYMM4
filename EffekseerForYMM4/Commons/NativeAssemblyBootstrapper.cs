using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace EffekseerForYMM4.Commons;

internal static class NativeAssemblyBootstrapper
{
    private const string NativeAssemblyName = "EffekseerForNative";
    private const string NativeAssemblyFileName = $"{NativeAssemblyName}.dll";
    private static readonly string PluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
    private static readonly string PayloadDirectory = Path.Combine(PluginDirectory, "nativepayload");
    private static readonly string CacheRootDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YukkuriMovieMaker",
        "PluginCache",
        "EffekseerForYMM4");

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
        var nativeAssemblyPath = GetNativeAssemblyPath();
        if (nativeAssemblyPath == null || !File.Exists(nativeAssemblyPath))
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

        return AssemblyLoadContext.Default.LoadFromAssemblyPath(nativeAssemblyPath);
    }

    private static void PrepareNativeFiles()
    {
        if (!Directory.Exists(PayloadDirectory))
        {
            return;
        }

        var cacheDirectory = GetCacheDirectory();
        if (cacheDirectory == null)
        {
            return;
        }

        Directory.CreateDirectory(cacheDirectory);
        CopyPayload(cacheDirectory, "EffekseerForNative.bin", "EffekseerForNative.dll");
        CopyPayload(cacheDirectory, "Ijwhost.bin", "Ijwhost.dll");
        CopyPayload(cacheDirectory, "EffekseerForNative.pdb.bin", "EffekseerForNative.pdb");
    }

    private static string? GetNativeAssemblyPath()
    {
        var cacheDirectory = GetCacheDirectory();
        if (cacheDirectory == null)
        {
            return null;
        }

        return Path.Combine(cacheDirectory, NativeAssemblyFileName);
    }

    private static string? GetCacheDirectory()
    {
        var payloadFingerprint = ComputePayloadFingerprint();
        if (payloadFingerprint == null)
        {
            return null;
        }

        return Path.Combine(CacheRootDirectory, payloadFingerprint);
    }

    private static string? ComputePayloadFingerprint()
    {
        var sourcePath = Path.Combine(PayloadDirectory, "EffekseerForNative.bin");
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var fileName in new[] { "EffekseerForNative.bin", "Ijwhost.bin", "EffekseerForNative.pdb.bin" })
        {
            var path = Path.Combine(PayloadDirectory, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            incrementalHash.AppendData(System.Text.Encoding.UTF8.GetBytes(fileName));
            using var stream = File.OpenRead(path);
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                incrementalHash.AppendData(buffer, 0, bytesRead);
            }
        }

        return Convert.ToHexString(incrementalHash.GetHashAndReset());
    }

    private static void CopyPayload(string cacheDirectory, string sourceFileName, string destinationFileName)
    {
        var sourcePath = Path.Combine(PayloadDirectory, sourceFileName);
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var destinationPath = Path.Combine(cacheDirectory, destinationFileName);
        var shouldCopy = !File.Exists(destinationPath);
        if (shouldCopy)
        {
            try
            {
                File.Copy(sourcePath, destinationPath, false);
            }
            catch (IOException) when (File.Exists(destinationPath))
            {
            }
            catch (UnauthorizedAccessException) when (File.Exists(destinationPath))
            {
            }
        }
    }
}
