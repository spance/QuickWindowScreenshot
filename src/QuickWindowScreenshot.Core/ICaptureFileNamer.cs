namespace QuickWindowScreenshot;

internal interface ICaptureFileNamer
{
    string BuildFilename(string prefix);
}
