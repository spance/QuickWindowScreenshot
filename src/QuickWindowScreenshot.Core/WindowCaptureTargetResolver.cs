namespace QuickWindowScreenshot;

internal sealed class WindowCaptureTargetResolver : ICaptureTargetResolver
{
    public CaptureTarget Resolve(CaptureRequest request)
    {
        string targetMode = CaptureTargetModes.Normalize(request.TargetMode);
        if (targetMode == CaptureTargetModes.FullScreenTarget && request.Hwnd == IntPtr.Zero)
        {
            DisplayInfo cursorDisplay = DisplayService.FromCursorPosition();
            return new CaptureTarget(IntPtr.Zero, cursorDisplay.Bounds, cursorDisplay);
        }

        if (!WindowService.IsValidWindow(request.Hwnd))
        {
            throw new InvalidOperationException("目标窗口不存在或句柄已失效，请重新选择窗口");
        }

        if (request.BringToFront && !WindowService.BringToForegroundAndWait(request.Hwnd))
        {
            throw new InvalidOperationException("无法前置目标窗口，截图已取消；请手动切换到目标窗口或关闭窗口前置选项");
        }

        if (WindowService.IsWindowMinimized(request.Hwnd))
        {
            throw new InvalidOperationException("目标窗口已最小化，无法捕获客户区");
        }

        Rectangle contentRect = WindowService.GetContentRectScreen(request.Hwnd);
        if (contentRect.Width <= 0 || contentRect.Height <= 0)
        {
            throw new InvalidOperationException("目标窗口客户区尺寸无效");
        }

        DisplayInfo? display = DisplayService.GetContainingDisplay(contentRect);
        if (display is null)
        {
            throw new InvalidOperationException("客户区必须完整位于同一个显示器内；请确认窗口没有跨屏或超出屏幕边缘");
        }

        Rectangle rect = targetMode == CaptureTargetModes.FullScreenTarget
            ? display.Bounds
            : contentRect;

        return new CaptureTarget(request.Hwnd, rect, display);
    }
}
