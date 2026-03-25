using System.Windows;

namespace EffekseerForYMM4.Commons
{
    internal sealed class EffekseerLoadErrorNotifier
    {
        private string lastShownErrorKey = string.Empty;

        public void Reset()
        {
            lastShownErrorKey = string.Empty;
        }

        public void ShowIfNeeded(string? filePath, string detail)
        {
            var safePath = filePath ?? string.Empty;
            var safeDetail = string.IsNullOrWhiteSpace(detail)
                ? Translate.Error_EffectFilesMayBeInvalid
                : detail.Trim();
            var errorKey = $"{safePath}|{safeDetail}";
            if (string.Equals(lastShownErrorKey, errorKey, StringComparison.Ordinal))
            {
                return;
            }

            lastShownErrorKey = errorKey;

            MessageBox.Show(
                $"{Translate.Error_EffectLoadFailed_Title}\n\n{Translate.Error_TargetPath_Label}: {safePath}\n\n{safeDetail}\n\n{Translate.Error_CheckEffectFiles}",
                Translate.Error_DialogTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
