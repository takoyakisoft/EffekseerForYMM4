using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EffekseerForYMM4.Diagnostics;

namespace EffekseerForYMM4.Commons;

internal static class NativeAssemblyBootstrapper
{
    internal const string NativeLibraryName = "EffekseerForNative";
    internal const string NativeLibraryFileName = $"{NativeLibraryName}.dll";
    private static readonly string PluginDirectory =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;

    [SuppressMessage("Usage", "CA2255:ModuleInitializer 属性はライブラリ コードで使用しないでください", Justification = "The native C ABI resolver must be registered before the first P/Invoke call.")]
    [ModuleInitializer]
    internal static void Initialize()
    {
        PluginLog.Initialize();
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(NativeAssemblyBootstrapper).Assembly, ResolveNativeLibrary);
        }
        catch (Exception exception)
        {
            PluginLog.Error("Native library resolver registration failed", exception);
            throw;
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

        var pluginPath = Path.Combine(PluginDirectory, NativeLibraryFileName);
        if (NativeLibrary.TryLoad(pluginPath, out var pluginHandle))
        {
            PluginLog.Information($"Native library loaded. path={pluginPath}");
            return pluginHandle;
        }

        var developmentPath = Path.Combine(AppContext.BaseDirectory, NativeLibraryFileName);
        if (!string.Equals(pluginPath, developmentPath, StringComparison.OrdinalIgnoreCase) &&
            NativeLibrary.TryLoad(developmentPath, out var developmentHandle))
        {
            PluginLog.Information($"Native library loaded from development path. path={developmentPath}");
            return developmentHandle;
        }

        PluginLog.Warning(
            $"Native library could not be loaded. pluginPath={pluginPath}, developmentPath={developmentPath}");
        return IntPtr.Zero;
    }
}
