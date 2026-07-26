using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using EffekseerForYMM4.Commons;

namespace EffekseerForYMM4.Diagnostics;

internal enum PluginLogLevel
{
    Debug,
    Information,
    Warning,
    Error,
}

internal static class PluginLog
{
    private const long MaximumFileBytes = 2 * 1024 * 1024;
    private const long RetainedFileBytes = 1024 * 1024;
    private const int MaximumMessageCharacters = 32 * 1024;
    private const string PluginName = "EffekseerForYMM4";
    private const string LogFileName = $"{PluginName}.log";
    private const string LogLevelEnvironmentVariable = "EFFEKSEERFORYMM4_LOG_LEVEL";

    private static readonly object InitializationSync = new();
    private static bool initialized;
    private static BoundedLogFile? file;
    private static PluginLogLevel minimumLevel;

    internal static string? FilePath => file?.Path;

    internal static void Initialize()
    {
        if (Volatile.Read(ref initialized))
        {
            return;
        }

        lock (InitializationSync)
        {
            if (initialized)
            {
                return;
            }

            try
            {
                var assembly = typeof(PluginLog).Assembly;
                var pluginDirectory = Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory;
                file = new BoundedLogFile(
                    Path.Combine(pluginDirectory, LogFileName),
                    MaximumFileBytes,
                    RetainedFileBytes);
                minimumLevel = ResolveMinimumLevel();

                var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
                var informationalVersion =
                    assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
                    string.Empty;
                var nativePath = Path.Combine(pluginDirectory, NativeAssemblyBootstrapper.NativeLibraryFileName);
                var message =
                    $"{PluginName} support metadata. " +
                    $"assemblyVersion={assembly.GetName().Version}, " +
                    $"fileVersion={versionInfo.FileVersion}, " +
                    $"informationalVersion={informationalVersion}, " +
                    $"processArchitecture={RuntimeInformation.ProcessArchitecture}, " +
                    $"assemblyPath={assembly.Location}, " +
                    $"nativePath={nativePath}, " +
                    $"configuredLogLevel={minimumLevel}";

#if DEBUG
                WriteCore(PluginLogLevel.Information, message, null);
#else
                WriteCore(PluginLogLevel.Warning, message, null);
#endif
            }
            catch
            {
                file = null;
            }
            finally
            {
                Volatile.Write(ref initialized, true);
            }
        }
    }

    internal static bool IsEnabled(PluginLogLevel level)
    {
        EnsureInitialized();
        return file != null && level >= minimumLevel;
    }

    internal static void Debug(string message) => Write(PluginLogLevel.Debug, message, null);

    internal static void Information(string message) => Write(PluginLogLevel.Information, message, null);

    internal static void Warning(string message, Exception? exception = null) =>
        Write(PluginLogLevel.Warning, message, exception);

    internal static void Error(string message, Exception? exception = null) =>
        Write(PluginLogLevel.Error, message, exception);

    internal static string FormatLine(
        DateTimeOffset timestamp,
        PluginLogLevel level,
        string message,
        Exception? exception)
    {
        var builder = new StringBuilder();
        builder.Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        builder.Append(' ');
        builder.Append(ToLevelText(level));
        builder.Append(' ');
        builder.Append(PluginName);
        builder.Append(' ');
        AppendClean(builder, message);

        if (exception != null)
        {
            builder.Append(level >= PluginLogLevel.Error ? ", exceptionDetail " : ", exception ");
            AppendClean(builder, level >= PluginLogLevel.Error ? exception.ToString() : exception.Message);
        }

        builder.Append('.');
        return builder.Length <= MaximumMessageCharacters
            ? builder.ToString()
            : string.Concat(builder.ToString(0, MaximumMessageCharacters - 4), "...");
    }

    private static void Write(PluginLogLevel level, string message, Exception? exception)
    {
        EnsureInitialized();
        WriteCore(level, message, exception);
    }

    private static void WriteCore(PluginLogLevel level, string message, Exception? exception)
    {
        if (file == null || level < minimumLevel)
        {
            return;
        }

        try
        {
            file.WriteLine(FormatLine(DateTimeOffset.Now, level, message, exception));
        }
        catch
        {
            // Diagnostics must never affect rendering or plugin loading.
        }
    }

    private static void EnsureInitialized()
    {
        if (!Volatile.Read(ref initialized))
        {
            Initialize();
        }
    }

    private static PluginLogLevel ResolveMinimumLevel()
    {
        var configured = Environment.GetEnvironmentVariable(LogLevelEnvironmentVariable);
        if (Enum.TryParse<PluginLogLevel>(configured, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

#if DEBUG
        return PluginLogLevel.Information;
#else
        return PluginLogLevel.Warning;
#endif
    }

    private static string ToLevelText(PluginLogLevel level) => level switch
    {
        PluginLogLevel.Debug => "debug",
        PluginLogLevel.Information => "info",
        PluginLogLevel.Warning => "warn",
        PluginLogLevel.Error => "error",
        _ => "info",
    };

    private static void AppendClean(StringBuilder builder, string text)
    {
        foreach (var character in text)
        {
            builder.Append(character switch
            {
                '{' or '}' or '[' or ']' or '"' or '\r' or '\n' or '\t' => ' ',
                _ => character,
            });
        }
    }
}
