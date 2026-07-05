using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using QuickWindowScreenshot;

namespace QuickWindowScreenshot.App.ViewModels;

internal sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly CaptureService _captureService = new();
    private readonly HotkeyManager _hotkeyManager = new();
    private AppSettings _settings = AppSettings.Load();
    private WindowInfo? _selectedWindow;
    private BackendOption _selectedBackend;
    private string _outputDir;
    private string _hotkey;
    private string _prefix;
    private string _targetMode;
    private string _statusText = "就绪";
    private string _diagnosticText = "";
    private double _intervalSeconds;
    private bool _bringToFront;
    private bool _isAutoMode;
    private bool _isAutoRunning;
    private bool _isCaptureInProgress;
    private bool _isPickingForeground;
    private bool _disposed;

    public MainViewModel()
    {
        Backends =
        [
            new("Windows Graphics Capture", CaptureBackendIds.Wgc),
            new("DXGI Desktop Duplication", CaptureBackendIds.Dxgi),
            new("GDI CopyFromScreen", CaptureBackendIds.Gdi),
        ];

        _outputDir = _settings.OutputDir;
        _hotkey = _settings.Hotkey;
        _intervalSeconds = (double)_settings.IntervalSeconds;
        _prefix = _settings.Prefix;
        _bringToFront = _settings.BringToFront;
        _targetMode = _settings.TargetMode;
        _selectedBackend = Backends.First(item => item.Value == _settings.Backend);
        UpdateFilenamePreview();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WindowInfo> Windows { get; } = [];

    public IReadOnlyList<BackendOption> Backends { get; }

    public WindowInfo? SelectedWindow
    {
        get => _selectedWindow;
        set
        {
            if (SetProperty(ref _selectedWindow, value))
            {
                UpdateSelectedWindowInfo(updateStatus: false);
            }
        }
    }

    public BackendOption SelectedBackend
    {
        get => _selectedBackend;
        set => SetProperty(ref _selectedBackend, value);
    }

    public string OutputDir
    {
        get => _outputDir;
        set => SetProperty(ref _outputDir, value);
    }

    public string Hotkey
    {
        get => _hotkey;
        set => SetProperty(ref _hotkey, value);
    }

    public double IntervalSeconds
    {
        get => _intervalSeconds;
        set
        {
            double normalized = double.IsFinite(value) ? Math.Clamp(value, 1, 86400) : (double)AppSettings.DefaultIntervalSeconds;
            SetProperty(ref _intervalSeconds, normalized);
        }
    }

    public string Prefix
    {
        get => _prefix;
        set
        {
            if (SetProperty(ref _prefix, value))
            {
                UpdateFilenamePreview();
            }
        }
    }

    public string FileNamePreview { get; private set; } = "";

    public bool BringToFront
    {
        get => _bringToFront;
        set => SetProperty(ref _bringToFront, value);
    }

    public bool IsWindowContentTarget
    {
        get => _targetMode == CaptureTargetModes.WindowContent;
        set
        {
            if (value)
            {
                SetTargetMode(CaptureTargetModes.WindowContent);
            }
        }
    }

    public bool IsFullScreenTarget
    {
        get => _targetMode == CaptureTargetModes.FullScreenTarget;
        set
        {
            if (value)
            {
                SetTargetMode(CaptureTargetModes.FullScreenTarget);
            }
        }
    }

    public bool IsManualMode
    {
        get => !_isAutoMode;
        set
        {
            if (value)
            {
                SetAutoMode(false);
            }
        }
    }

    public bool IsAutoMode
    {
        get => _isAutoMode;
        set
        {
            if (value)
            {
                SetAutoMode(true);
            }
        }
    }

    public bool IsAutoRunning
    {
        get => _isAutoRunning;
        private set
        {
            if (SetProperty(ref _isAutoRunning, value))
            {
                NotifyControlStateChanged();
            }
        }
    }

    public bool CaptureButtonEnabled => !_isCaptureInProgress;

    public bool StartAutoButtonEnabled => IsAutoMode && !IsAutoRunning && !_isCaptureInProgress;

    public bool StopAutoButtonEnabled => IsAutoRunning;

    public bool AutoSettingsEnabled => IsAutoMode && !IsAutoRunning && !_isCaptureInProgress;

    public bool BringToFrontOptionEnabled => !_isCaptureInProgress;

    public bool TargetWindowControlsEnabled =>
        IsWindowContentTarget && !_isCaptureInProgress && !_isPickingForeground;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string DiagnosticText
    {
        get => _diagnosticText;
        private set => SetProperty(ref _diagnosticText, value);
    }

    public string VersionText => AppVersion.DisplayText;

    public void RefreshWindows()
    {
        IntPtr current = SelectedWindow?.Hwnd ?? IntPtr.Zero;
        Windows.Clear();

        try
        {
            foreach (WindowInfo window in WindowService.EnumerateWindows())
            {
                Windows.Add(window);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"窗口枚举失败: {ex.Message}";
            return;
        }

        SelectedWindow = Windows.FirstOrDefault(item => current != IntPtr.Zero && item.Hwnd == current)
            ?? Windows.FirstOrDefault();
        StatusText = $"已刷新窗口列表: {Windows.Count} 个";
        UpdateSelectedWindowInfo(updateStatus: false);
    }

    public async Task CaptureAsync(bool automatic = false)
    {
        if (_isCaptureInProgress)
        {
            if (!automatic)
            {
                StatusText = "截图仍在进行中";
            }
            return;
        }

        AppSettings settings = CollectSettings();
        bool fullScreenTarget = settings.TargetMode == CaptureTargetModes.FullScreenTarget;
        IntPtr hwnd = fullScreenTarget ? IntPtr.Zero : SelectedWindow?.Hwnd ?? IntPtr.Zero;
        if (!fullScreenTarget && hwnd == IntPtr.Zero)
        {
            StatusText = "请先选择目标窗口";
            return;
        }
        if (!fullScreenTarget && !WindowService.IsValidWindow(hwnd))
        {
            StatusText = "目标窗口已关闭或句柄失效，请重新选择窗口";
            RefreshWindows();
            if (automatic)
            {
                StopAutoCapture();
            }
            return;
        }

        CaptureRequest request = new(
            hwnd,
            settings.OutputDir,
            settings.Prefix,
            settings.Backend,
            settings.BringToFront,
            settings.TargetMode);

        SetCaptureInProgress(true);
        try
        {
            CaptureResult result = await Task.Run(() => _captureService.Capture(request));
            StatusText = $"已保存: {Path.GetFileName(result.Path)}  {result.Size.Width}x{result.Size.Height}  {result.Backend}";
            UpdateSelectedWindowInfo(updateStatus: false);
            SaveCurrentSettings();
        }
        catch (Exception ex)
        {
            StatusText = $"截图失败: {ex.Message}";
        }
        finally
        {
            SetCaptureInProgress(false);
        }
    }

    public bool StartAutoCapture()
    {
        AppSettings settings = CollectSettings();
        bool fullScreenTarget = settings.TargetMode == CaptureTargetModes.FullScreenTarget;
        IntPtr hwnd = fullScreenTarget ? IntPtr.Zero : SelectedWindow?.Hwnd ?? IntPtr.Zero;
        if (!fullScreenTarget && hwnd == IntPtr.Zero)
        {
            StatusText = "请先选择目标窗口";
            return false;
        }
        if (!fullScreenTarget && !WindowService.IsValidWindow(hwnd))
        {
            StatusText = "目标窗口已关闭或句柄失效，请重新选择窗口";
            RefreshWindows();
            return false;
        }

        IsAutoMode = true;
        IsAutoRunning = true;
        StatusText = "自动截图已启动";
        SaveCurrentSettings();
        return true;
    }

    public void StopAutoCapture()
    {
        if (!IsAutoRunning)
        {
            return;
        }

        IsAutoRunning = false;
        StatusText = "自动截图已停止";
    }

    public bool BeginPickingForeground(string statusText)
    {
        if (_isPickingForeground)
        {
            return false;
        }

        _isPickingForeground = true;
        StatusText = statusText;
        NotifyControlStateChanged();
        return true;
    }

    public void EndPickingForeground()
    {
        _isPickingForeground = false;
        NotifyControlStateChanged();
    }

    public void SelectForegroundWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            StatusText = "未取得前台窗口";
            return;
        }

        RefreshWindows();
        WindowInfo? existing = Windows.FirstOrDefault(item => item.Hwnd == hwnd);
        if (existing is not null)
        {
            SelectedWindow = existing;
            StatusText = $"已选中前台窗口: {FriendlyWindowName(existing)}";
            return;
        }

        WindowInfo? window = WindowService.CreateWindowInfo(hwnd);
        if (window is not null)
        {
            Windows.Add(window);
            SelectedWindow = window;
            StatusText = $"已选中前台窗口: {FriendlyWindowName(window)}";
            return;
        }

        StatusText = "前台窗口不可捕获";
    }

    public void ApplyHotkey(IntPtr hwnd)
    {
        try
        {
            _hotkeyManager.Register(hwnd, Hotkey);
            Hotkey = _hotkeyManager.Sequence;
            StatusText = $"热键已注册: {Hotkey}";
            SaveCurrentSettings();
        }
        catch (Exception ex)
        {
            StatusText = $"热键注册失败: {ex.Message}";
        }
    }

    public void SaveCurrentSettings()
    {
        _settings = CollectSettings();
        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            StatusText = $"设置保存失败: {ex.Message}";
        }
    }

    public void OpenOutputFolder()
    {
        string directory = string.IsNullOrWhiteSpace(OutputDir) ? WindowService.DefaultOutputDir() : OutputDir.Trim();
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo(directory)
        {
            UseShellExecute = true,
        });
    }

    public void UpdateSelectedWindowInfo(bool updateStatus = true)
    {
        if (IsFullScreenTarget)
        {
            NativeMethods.GetCursorPos(out NativeMethods.POINT cursor);
            DisplayInfo display = DisplayService.FromPoint(new Point(cursor.X, cursor.Y));
            Rectangle bounds = display.Bounds;
            DiagnosticText =
                $"targetMode={CaptureTargetModes.FullScreenTarget}"
                + $"{Environment.NewLine}screen={display.DeviceName}; bounds={bounds.Width}x{bounds.Height} "
                + $"({bounds.Left},{bounds.Top})-({bounds.Right},{bounds.Bottom})"
                + $"{Environment.NewLine}cursor=({cursor.X},{cursor.Y})";
            if (updateStatus)
            {
                StatusText = $"就绪 · 全屏目标: 鼠标所在屏幕 {bounds.Width}x{bounds.Height} · 输出到 {ShortPath(OutputDir)}";
            }
            return;
        }

        WindowInfo? window = SelectedWindow;
        if (window is null)
        {
            DiagnosticText = "not selected";
            if (updateStatus)
            {
                StatusText = "就绪 · 未选择目标窗口";
            }
            return;
        }

        Rectangle client = window.ClientRect;
        if (updateStatus)
        {
            StatusText = $"就绪 · 已选择: {FriendlyWindowName(window)} · 窗口内容 · 输出到 {ShortPath(OutputDir)}";
        }

        string stateLine =
            $"hwnd=0x{window.Hwnd.ToInt64():X}; pid={window.Pid}; hidden={!NativeMethods.IsWindowVisible(window.Hwnd)}; minimized={window.Minimized}";
        string rectLine = "contentRect=n/a; inset=n/a";
        try
        {
            Rectangle content = WindowService.GetContentRectScreen(window.Hwnd);
            Rectangle frame = WindowService.GetVisibleFrameRectScreen(window.Hwnd);
            int insetLeft = content.Left - frame.Left;
            int insetTop = content.Top - frame.Top;
            int insetRight = frame.Right - content.Right;
            int insetBottom = frame.Bottom - content.Bottom;
            Rectangle? infoRect = WindowService.GetWindowInfoClientRectScreen(window.Hwnd);
            string infoText = infoRect.HasValue && infoRect.Value != client
                ? $" rcClient=({infoRect.Value.Left},{infoRect.Value.Top})-({infoRect.Value.Right},{infoRect.Value.Bottom})"
                : "";
            string className = WindowService.GetClassName(window.Hwnd);
            stateLine =
                $"hwnd=0x{window.Hwnd.ToInt64():X}; pid={window.Pid}; hidden={!NativeMethods.IsWindowVisible(window.Hwnd)}; "
                + $"minimized={window.Minimized}; valid={WindowService.IsValidWindow(window.Hwnd)}; targetMode={_targetMode}; "
                + $"dpi={NativeMethods.DpiAwarenessStatus}; class={className}";
            rectLine =
                $"contentRect={content.Width}x{content.Height} ({content.Left},{content.Top})-({content.Right},{content.Bottom}); "
                + $"inset=L{insetLeft},T{insetTop},R{insetRight},B{insetBottom}{infoText}";
        }
        catch
        {
        }

        DiagnosticText =
            $"clientRect={client.Width}x{client.Height} ({client.Left},{client.Top})-({client.Right},{client.Bottom})"
            + $"{Environment.NewLine}{rectLine}"
            + $"{Environment.NewLine}{stateLine}";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        SaveCurrentSettings();
        _hotkeyManager.Unregister();
        _captureService.Dispose();
        _disposed = true;
    }

    private AppSettings CollectSettings() => AppSettings.Normalize(new AppSettings
    {
        OutputDir = string.IsNullOrWhiteSpace(OutputDir) ? WindowService.DefaultOutputDir() : OutputDir.Trim(),
        Hotkey = string.IsNullOrWhiteSpace(Hotkey) ? _settings.Hotkey : Hotkey.Trim(),
        IntervalSeconds = (decimal)Math.Clamp(IntervalSeconds, 1, 86400),
        Prefix = string.IsNullOrWhiteSpace(Prefix) ? AppSettings.DefaultPrefix : Prefix.Trim(),
        Backend = SelectedBackend.Value,
        BringToFront = BringToFront,
        TargetMode = _targetMode,
    });

    private void SetTargetMode(string targetMode)
    {
        targetMode = CaptureTargetModes.Normalize(targetMode);
        if (_targetMode == targetMode)
        {
            return;
        }

        _targetMode = targetMode;
        OnPropertyChanged(nameof(IsWindowContentTarget));
        OnPropertyChanged(nameof(IsFullScreenTarget));
        NotifyControlStateChanged();
        UpdateSelectedWindowInfo();
    }

    private void SetAutoMode(bool isAutoMode)
    {
        if (_isAutoMode == isAutoMode)
        {
            return;
        }

        _isAutoMode = isAutoMode;
        OnPropertyChanged(nameof(IsManualMode));
        OnPropertyChanged(nameof(IsAutoMode));
        NotifyControlStateChanged();
    }

    private void SetCaptureInProgress(bool value)
    {
        if (_isCaptureInProgress == value)
        {
            return;
        }

        _isCaptureInProgress = value;
        NotifyControlStateChanged();
    }

    private void NotifyControlStateChanged()
    {
        OnPropertyChanged(nameof(CaptureButtonEnabled));
        OnPropertyChanged(nameof(StartAutoButtonEnabled));
        OnPropertyChanged(nameof(StopAutoButtonEnabled));
        OnPropertyChanged(nameof(AutoSettingsEnabled));
        OnPropertyChanged(nameof(BringToFrontOptionEnabled));
        OnPropertyChanged(nameof(TargetWindowControlsEnabled));
    }

    private void UpdateFilenamePreview()
    {
        string prefix = string.IsNullOrWhiteSpace(Prefix) ? AppSettings.DefaultPrefix : Prefix.Trim();
        FileNamePreview = new TimestampedCaptureFileNamer(() => new DateTime(2026, 7, 3, 16, 7, 2, 123))
            .BuildFilename(prefix);
        OnPropertyChanged(nameof(FileNamePreview));
    }

    private static string FriendlyWindowName(WindowInfo window)
    {
        string title = string.IsNullOrWhiteSpace(window.Title) ? "[untitled]" : window.Title;
        string process = string.IsNullOrWhiteSpace(window.ProcessName) ? "unknown" : window.ProcessName;
        return $"{title} · {process} · {window.ClientRect.Width}x{window.ClientRect.Height}";
    }

    private static string ShortPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "(未设置)";
        }

        string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? trimmed : name;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
