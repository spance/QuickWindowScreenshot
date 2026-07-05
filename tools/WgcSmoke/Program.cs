using System.Drawing;
using QuickWindowScreenshot;

Application.EnableVisualStyles();
NativeMethods.EnableDpiAwareness();

using Form form = new()
{
    Text = "WGC Smoke Target",
    StartPosition = FormStartPosition.Manual,
    Location = new Point(120, 120),
    ClientSize = new Size(320, 180),
    BackColor = Color.RoyalBlue,
    TopMost = true,
};
Label label = new()
{
    Dock = DockStyle.Fill,
    Text = "WGC",
    TextAlign = ContentAlignment.MiddleCenter,
    ForeColor = Color.White,
    Font = new Font("Segoe UI", 32, FontStyle.Bold),
};
form.Controls.Add(label);
form.Show();
Application.DoEvents();
Thread.Sleep(300);

using CaptureService service = new();
string output = Path.Combine(Path.GetTempPath(), "QuickWindowScreenshot-WgcSmoke");
CaptureResult result = service.Capture(new CaptureRequest(
    form.Handle,
    output,
    "wgc-smoke",
    CaptureBackendIds.Wgc,
    false,
    CaptureTargetModes.WindowContent));
using Bitmap bitmap = new(result.Path);
Console.WriteLine(result.Path);
Console.WriteLine($"{bitmap.Width}x{bitmap.Height}");
if (bitmap.Width != form.ClientSize.Width || bitmap.Height != form.ClientSize.Height)
{
    throw new InvalidOperationException($"Unexpected size {bitmap.Width}x{bitmap.Height}, expected {form.ClientSize.Width}x{form.ClientSize.Height}");
}
