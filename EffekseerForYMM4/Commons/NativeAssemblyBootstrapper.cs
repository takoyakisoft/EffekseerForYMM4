using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EffekseerForYMM4.Commons;

internal static class NativeAssemblyBootstrapper
{
    internal const string NativeLibraryName = "EffekseerForNative";
    private const string NativeLibraryFileName = $"{NativeLibraryName}.dll";
    private static readonly string PluginDirectory =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;

    [SuppressMessage("Usage", "CA2255:ModuleInitializer 属性はライブラリ コードで使用しないでください", Justification = "The native C ABI resolver must be registered before the first P/Invoke call.")]
    [ModuleInitializer]
    internal static void Initialize()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeAssemblyBootstrapper).Assembly, ResolveNativeLibrary);
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        _ = assembly;
        _ = searchPath;
        if (!string.Equals(libraryName, NativeLibraryName, StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        var pluginPath = Path.Combine(PluginDirectory, NativeLibraryFileName);
        if (NativeLibrary.TryLoad(pluginPath, out var pluginHandle))
        {
            return pluginHandle;
        }

        var developmentPath = Path.Combine(AppContext.BaseDirectory, NativeLibraryFileName);
        return !string.Equals(pluginPath, developmentPath, StringComparison.OrdinalIgnoreCase) &&
            NativeLibrary.TryLoad(developmentPath, out var developmentHandle)
            ? developmentHandle
            : IntPtr.Zero;
    }
}
