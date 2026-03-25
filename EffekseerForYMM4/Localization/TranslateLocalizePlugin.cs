using System.Globalization;
using YukkuriMovieMaker.Plugin;

namespace EffekseerForYMM4;

public sealed class TranslateLocalizePlugin : ILocalizePlugin
{
    public string Name => Translate.Plugin_VideoEffect_Name;

    public void SetCulture(CultureInfo cultureInfo)
    {
        Translate.Culture = cultureInfo;
    }
}
