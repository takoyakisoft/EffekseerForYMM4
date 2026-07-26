using System.IO;
using System.Linq;

namespace EffekseerForYMM4.Tests;

public sealed class UnicodePathInteropTests
{
    public static TheoryData<string> FileNames =>
    [
        "test_ascii.efkefc",
        "\u30C6\u30B9\u30C8.efkefc",
        "\u5168\u89D2\u3000\u30B9\u30DA\u30FC\u30B9.efkefc",
        "emoji_\U0001F60A.efkefc",
        "cp932\u5916_\u20AC_\U00020BB7.efkefc",
        "e\u0301_\u00E9.efkefc",
        Path.Combine("\u65E5\u672C\u8A9E\u30D5\u30A9\u30EB\u30C0", "emoji_\U0001F680.efkefc"),
        Path.Combine(
            Enumerable
                .Repeat("long-path-segment", 20)
                .Append("\u9577\u3044\u30D1\u30B9.efkefc")
                .ToArray()),
    ];

    [Theory]
    [MemberData(nameof(FileNames))]
    public void LoadEffect_AcceptsUnicodePathAcrossNativeAbi(string relativePath)
    {
        using var renderer = new EffekseerForNative.EffekseerRenderer();
        var path = Path.Combine(Path.GetTempPath(), relativePath);

        Assert.False(renderer.LoadEffect(path));
    }
}
