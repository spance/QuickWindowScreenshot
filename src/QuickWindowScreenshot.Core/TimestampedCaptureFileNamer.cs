using System.Text.RegularExpressions;

namespace QuickWindowScreenshot;

internal sealed class TimestampedCaptureFileNamer : ICaptureFileNamer
{
    private readonly Func<DateTime> _now;

    public TimestampedCaptureFileNamer()
        : this(() => DateTime.Now)
    {
    }

    internal TimestampedCaptureFileNamer(Func<DateTime> now)
    {
        _now = now;
    }

    public string BuildFilename(string prefix)
    {
        string cleanPrefix = Regex.Replace(prefix.Trim(), @"[^A-Za-z0-9._-]+", "_");
        if (string.IsNullOrWhiteSpace(cleanPrefix))
        {
            cleanPrefix = AppSettings.DefaultPrefix;
        }

        string timestamp = _now().ToString("yyyyMMdd_HHmmss_fff");
        return $"{cleanPrefix}_{timestamp}.png";
    }
}
