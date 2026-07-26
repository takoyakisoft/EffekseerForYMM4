using System.IO;
using System.Text;
using EffekseerForYMM4.Diagnostics;

namespace EffekseerForYMM4.Tests.Diagnostics;

public sealed class BoundedLogFileTests
{
    [Fact]
    public void WriteLineCompactsOldLinesAndKeepsOneReadableLogFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"EffekseerForYMM4.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "EffekseerForYMM4.log");
            var file = new BoundedLogFile(path, maximumBytes: 160, retainedBytes: 70);

            for (var index = 0; index < 12; index++)
            {
                file.WriteLine($"line-{index:D2}-abcdefghijklmnopqrstuvwxyz");
            }

            var text = File.ReadAllText(path, Encoding.UTF8);
            Assert.Contains("line-11", text, StringComparison.Ordinal);
            Assert.DoesNotContain("line-00", text, StringComparison.Ordinal);
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FormatterUsesPlainSingleLineVtuberKitStyle()
    {
        var line = PluginLog.FormatLine(
            new DateTimeOffset(2026, 7, 26, 20, 15, 30, 123, TimeSpan.FromHours(9)),
            PluginLogLevel.Warning,
            "Native failed.\npath={sample}",
            new InvalidOperationException("failure"));

        Assert.Equal(
            "2026-07-26 20:15:30.123 warn EffekseerForYMM4 Native failed. path= sample , exception failure.",
            line);
    }
}
