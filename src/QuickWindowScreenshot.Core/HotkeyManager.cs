namespace QuickWindowScreenshot;

internal sealed class HotkeyManager
{
    private IntPtr _handle;

    public bool Registered { get; private set; }

    public string Sequence { get; private set; } = "";

    public void Register(IntPtr handle, string sequence)
    {
        Unregister();
        HotkeyDefinition definition = HotkeyDefinition.Parse(sequence);
        if (!NativeMethods.RegisterHotKey(handle, NativeMethods.HOTKEY_ID, definition.Modifiers, definition.Key))
        {
            throw NativeMethods.LastWin32Exception();
        }

        _handle = handle;
        Sequence = definition.Text;
        Registered = true;
    }

    public void Unregister()
    {
        if (Registered)
        {
            NativeMethods.UnregisterHotKey(_handle, NativeMethods.HOTKEY_ID);
        }

        Registered = false;
    }
}
