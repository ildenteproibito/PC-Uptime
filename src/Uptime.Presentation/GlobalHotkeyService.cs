using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace Uptime.Presentation;

/// <summary>Registers Win+Alt+U and pumps the WM_HOTKEY thread message on the UI dispatcher.</summary>
internal sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x5550;
    private const uint ModAlt = 0x0001, ModWin = 0x0008, VkU = 0x55;
    private const uint WmHotkey = 0x0312, PmRemove = 0x0001;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly Action _onPressed;
    private bool _registered;

    public GlobalHotkeyService(Action onPressed)
    {
        _onPressed = onPressed;
        _registered = RegisterHotKey(IntPtr.Zero, HotkeyId, ModWin | ModAlt, VkU);
        if (_registered) { _timer.Tick += Poll; _timer.Start(); }
    }

    private void Poll(object? sender, EventArgs e)
    {
        while (PeekMessage(out var message, IntPtr.Zero, WmHotkey, WmHotkey, PmRemove)) _onPressed();
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_registered) UnregisterHotKey(IntPtr.Zero, HotkeyId);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage { public IntPtr HWnd; public uint Message; public UIntPtr WParam; public IntPtr LParam; public uint Time; public int X, Y; }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out NativeMessage message, IntPtr hWnd, uint min, uint max, uint remove);
}
