using System.IO;
using System.Text;

namespace EffekseerForYMM4.Diagnostics;

internal sealed class BoundedLogFile(string path, long maximumBytes, long retainedBytes)
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private readonly Lock sync = new();

    internal string Path { get; } = path;

    internal void WriteLine(string line)
    {
        lock (sync)
        {
            var lineBytes = Utf8WithoutBom.GetByteCount(line) + Utf8WithoutBom.GetByteCount(Environment.NewLine);
            if (File.Exists(Path) && new FileInfo(Path).Length + lineBytes > maximumBytes)
            {
                Compact();
            }

            using var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, Utf8WithoutBom);
            writer.WriteLine(line);
        }
    }

    private void Compact()
    {
        var lines = File.ReadAllLines(Path, Utf8WithoutBom);
        var retained = new List<string>();
        long retainedByteCount = 0;

        for (var index = lines.Length - 1; index >= 0; index--)
        {
            var lineByteCount =
                Utf8WithoutBom.GetByteCount(lines[index]) +
                Utf8WithoutBom.GetByteCount(Environment.NewLine);
            if (retained.Count > 0 && retainedByteCount + lineByteCount > retainedBytes)
            {
                break;
            }

            retained.Add(lines[index]);
            retainedByteCount += lineByteCount;
        }

        retained.Reverse();
        File.WriteAllLines(Path, retained, Utf8WithoutBom);
    }
}
