using EffekseerForYMM4.Commons;

namespace EffekseerForNative;

public static class NativePathBridge
{
    public static string Combine(string basePath, string childPath) =>
        EffekseerRenderer.ReadUtf8Buffer((buffer, size) => NativeMethods.PathCombine(basePath, childPath, buffer, size))
        ?? string.Empty;

    public static bool EnsureDirectory(string path, out string? errorMessage)
    {
        var result = NativeMethods.PathEnsureDirectory(path) != 0;
        errorMessage = ReadLastError();
        return result;
    }

    public static bool Exists(string path) => NativeMethods.PathExists(path) != 0;

    public static bool WriteUtf8TextFile(string path, string content, out string? errorMessage)
    {
        var result = NativeMethods.PathWriteUtf8Text(path, content) != 0;
        errorMessage = ReadLastError();
        return result;
    }

    public static string? ReadUtf8TextFile(string path, out string? errorMessage)
    {
        var result = EffekseerRenderer.ReadUtf8Buffer((buffer, size) => NativeMethods.PathReadUtf8Text(path, buffer, size));
        errorMessage = ReadLastError();
        return result;
    }

    private static string? ReadLastError() =>
        EffekseerRenderer.ReadUtf8Buffer(NativeMethods.PathGetLastError);
}
