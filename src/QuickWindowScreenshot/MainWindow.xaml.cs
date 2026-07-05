using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using QuickWindowScreenshot;
using QuickWindowScreenshot.App.Platform;
using QuickWindowScreenshot.App.ViewModels;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinPoint = Windows.Foundation.Point;

namespace QuickWindowScreenshot.App;

public sealed partial class MainWindow : Window
{
    private const int DefaultWindowWidth = 470;
    private const int DefaultWindowHeight = 520;
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    private readonly MainViewModel _viewModel = new();
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _autoTimer;
    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly InputNonClientPointerSource _nonClientPointerSource;
    private WindowMessageSubclass? _messageSubclass;

    public MainWindow()
    {
        InitializeComponent();

        Root.DataContext = _viewModel;
        _hwnd = WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd));
        _nonClientPointerSource = InputNonClientPointerSource.GetForWindowId(_appWindow.Id);
        _autoTimer = DispatcherQueue.CreateTimer();
        _autoTimer.Tick += AutoTimer_Tick;

        ConfigureTitleBar();
        ConfigureWindow();
        _messageSubclass = new WindowMessageSubclass(_hwnd, TryHandleWindowMessage);
        Closed += MainWindow_Closed;

        _viewModel.RefreshWindows();
        _viewModel.ApplyHotkey(_hwnd);
    }

    private void ConfigureWindow()
    {
        double scale = CurrentScale();
        int physicalWidth = (int)Math.Round(DefaultWindowWidth * scale);
        int physicalHeight = (int)Math.Round(DefaultWindowHeight * scale);

        _appWindow.Resize(new SizeInt32(physicalWidth, physicalHeight));
        DisplayArea displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest);
        RectInt32 workArea = displayArea.WorkArea;
        int x = workArea.X + Math.Max(0, (workArea.Width - physicalWidth) / 2);
        int y = workArea.Y + Math.Max(0, (workArea.Height - physicalHeight) / 2);
        _appWindow.Move(new PointInt32(x, y));
    }

    private void ConfigureTitleBar()
    {
        ExtendsContentIntoTitleBar = true;

        AppWindowTitleBar titleBar = _appWindow.TitleBar;
        titleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        titleBar.BackgroundColor = Colors.Transparent;
        titleBar.InactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        SizeChanged += (_, _) => UpdateTitleBarLayout();
        AppTitleBar.SizeChanged += (_, _) => UpdateNonClientRegions();
        TitleTabs.SizeChanged += (_, _) => UpdateNonClientRegions();
        TitleBarDragRegion.SizeChanged += (_, _) => UpdateNonClientRegions();
        UpdateTitleBarLayout();
        UpdateNonClientRegions();
    }

    private void UpdateTitleBarLayout()
    {
        AppWindowTitleBar titleBar = _appWindow.TitleBar;
        double scale = CurrentScale();
        TitleBarLeftInset.Width = new GridLength(titleBar.LeftInset / scale);
        TitleBarRightInset.Width = new GridLength(titleBar.RightInset / scale);
        TitleBarRow.Height = new GridLength(Math.Max(42, titleBar.Height / scale));
        UpdateNonClientRegions();
    }

    private void UpdateNonClientRegions()
    {
        RectInt32 tabRect = GetElementRect(TitleTabs);
        RectInt32 dragRect = GetElementRect(TitleBarDragRegion);
        _nonClientPointerSource.SetRegionRects(
            NonClientRegionKind.Passthrough,
            tabRect.Width > 0 && tabRect.Height > 0 ? [tabRect] : []);
        _nonClientPointerSource.SetRegionRects(
            NonClientRegionKind.Caption,
            dragRect.Width > 0 && dragRect.Height > 0 ? [dragRect] : []);
    }

    private RectInt32 GetElementRect(FrameworkElement element)
    {
        if (element.XamlRoot is null || element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return new RectInt32();
        }

        GeneralTransform transform = element.TransformToVisual(Root);
        WinPoint origin = transform.TransformPoint(new WinPoint(0, 0));
        double scale = CurrentScale();
        return new RectInt32(
            (int)Math.Round(origin.X * scale),
            (int)Math.Round(origin.Y * scale),
            Math.Max(0, (int)Math.Round(element.ActualWidth * scale)),
            Math.Max(0, (int)Math.Round(element.ActualHeight * scale)));
    }

    private double CurrentScale()
    {
        uint dpi = GetDpiForWindow(_hwnd);
        return dpi > 0 ? dpi / 96.0 : Root.XamlRoot?.RasterizationScale ?? 1;
    }

    private bool TryHandleWindowMessage(int message, IntPtr wParam, IntPtr lParam)
    {
        if (message == NativeMethods.WM_HOTKEY && wParam.ToInt32() == NativeMethods.HOTKEY_ID)
        {
            _ = _viewModel.CaptureAsync();
            return true;
        }

        return false;
    }

    private void TitleTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CapturePage is null || SettingsPage is null)
        {
            return;
        }

        bool settingsVisible = TitleTabs.SelectedIndex == 1;
        CapturePage.Visibility = settingsVisible ? Visibility.Collapsed : Visibility.Visible;
        SettingsPage.Visibility = settingsVisible ? Visibility.Visible : Visibility.Collapsed;
        _viewModel.UpdateSelectedWindowInfo(updateStatus: false);
    }

    private void RefreshWindows_Click(object sender, RoutedEventArgs e) => _viewModel.RefreshWindows();

    private async void CaptureNow_Click(object sender, RoutedEventArgs e) => await _viewModel.CaptureAsync();

    private void StartAuto_Click(object sender, RoutedEventArgs e)
    {
        int seconds = Math.Max(1, (int)Math.Round(_viewModel.IntervalSeconds));
        _viewModel.IntervalSeconds = seconds;
        _autoTimer.Interval = TimeSpan.FromSeconds(seconds);
        if (_viewModel.StartAutoCapture())
        {
            _autoTimer.Start();
        }
    }

    private void StopAuto_Click(object sender, RoutedEventArgs e)
    {
        _autoTimer.Stop();
        _viewModel.StopAutoCapture();
    }

    private void ManualMode_Checked(object sender, RoutedEventArgs e)
    {
        _autoTimer.Stop();
        _viewModel.StopAutoCapture();
    }

    private async void PickCurrentWindow_Click(object sender, RoutedEventArgs e) =>
        await PickForegroundWindowAsync(TimeSpan.FromMilliseconds(160), "正在选取当前前台窗口");

    private async void PickWindowAfterDelay_Click(object sender, RoutedEventArgs e) =>
        await PickForegroundWindowAsync(TimeSpan.FromSeconds(3), "请在 3 秒内切换到目标窗口");

    private async Task PickForegroundWindowAsync(TimeSpan delay, string statusText)
    {
        if (!_viewModel.BeginPickingForeground(statusText))
        {
            return;
        }

        IntPtr foreground = IntPtr.Zero;
        try
        {
            ShowWindow(_hwnd, SW_HIDE);
            await Task.Delay(delay);
            foreground = WindowService.GetForegroundWindow();
        }
        finally
        {
            ShowWindow(_hwnd, SW_SHOW);
            Activate();
            _viewModel.EndPickingForeground();
        }

        _viewModel.SelectForegroundWindow(foreground);
    }

    private async void BrowseOutputDir_Click(object sender, RoutedEventArgs e)
    {
        FolderPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, _hwnd);

        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            _viewModel.OutputDir = folder.Path;
            _viewModel.SaveCurrentSettings();
        }
    }

    private void ApplyHotkey_Click(object sender, RoutedEventArgs e) => _viewModel.ApplyHotkey(_hwnd);

    private void HotkeyBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        string text = HotkeyGestureFormatter.Format(e.Key);
        if (!string.IsNullOrWhiteSpace(text))
        {
            _viewModel.Hotkey = text;
            e.Handled = true;
        }
    }

    private void OpenOutputFolder_Click(object sender, RoutedEventArgs e) => _viewModel.OpenOutputFolder();

    private async void AutoTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        await _viewModel.CaptureAsync(automatic: true);
        if (!_viewModel.IsAutoRunning)
        {
            _autoTimer.Stop();
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _autoTimer.Stop();
        _messageSubclass?.Dispose();
        _messageSubclass = null;
        _viewModel.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
