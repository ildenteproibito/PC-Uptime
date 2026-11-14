using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Uptime.Presentation.Services;

/// <summary>
/// Native AOT-compatible Win32 system tray icon using Shell_NotifyIconW (replaces System.Windows.Forms.NotifyIcon).
/// </summary>
internal sealed class Win32TrayIcon : IDisposable
{
    private const int WmUser = 0x0400;
    private const int WmTrayCallback = WmUser + 1;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonUp = 0x0205;
    private const int WmContextMenu = 0x007B;

    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint NimSetVersion = 0x00000004;

    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NotifyIconVersion4 = 4;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private readonly WndProcDelegate _wndProc;
    private readonly uint _taskbarCreatedMsg;
    private readonly string _className;
    private IntPtr _hwnd;
    private IntPtr _hIcon;
    private string _tooltip = string.Empty;
    private bool _disposed;

    public event Action? LeftClicked;
    public event Action<int, int>? RightClicked;

    public string Text
    {
        get => _tooltip;
        set
        {
            var next = value ?? string.Empty;
            if (next.Length > 127) next = next[..127];
            if (_tooltip == next) return;
            _tooltip = next;
            if (_hwnd != IntPtr.Zero && !_disposed)
            {
                var data = CreateNotifyIconData(NifTip);
                data.szTip = _tooltip;
                Shell_NotifyIconW(NimModify, ref data);
            }
        }
    }

    public Win32TrayIcon(Stream icoStream, string initialTooltip)
    {
        _tooltip = initialTooltip.Length > 127 ? initialTooltip[..127] : initialTooltip;
        _hIcon = LoadIconFromStream(icoStream);
        _taskbarCreatedMsg = RegisterWindowMessageW("TaskbarCreated");
        _className = $"UptimeTrayWnd_{Guid.NewGuid():N}";
        _wndProc = WndProc;

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandleW(null),
            lpszClassName = _className
        };

        RegisterClassExW(ref wc);
        _hwnd = CreateWindowExW(0, _className, string.Empty, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        AddTrayIcon();
    }

    private void AddTrayIcon()
    {
        if (_hwnd == IntPtr.Zero) return;
        var data = CreateNotifyIconData(NifMessage | NifIcon | NifTip);
        data.uCallbackMessage = WmTrayCallback;
        data.hIcon = _hIcon;
        data.szTip = _tooltip;
        Shell_NotifyIconW(NimAdd, ref data);

        var ver = CreateNotifyIconData(0);
        ver.uVersion = NotifyIconVersion4;
        Shell_NotifyIconW(NimSetVersion, ref ver);
    }

    private NOTIFYICONDATAW CreateNotifyIconData(uint flags) => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = _hwnd,
        uID = 1,
        uFlags = flags
    };

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == _taskbarCreatedMsg && _taskbarCreatedMsg != 0)
        {
            AddTrayIcon();
            return IntPtr.Zero;
        }

        if (msg == WmTrayCallback)
        {
            var eventId = (int)(lParam.ToInt64() & 0xFFFF);
            if (eventId == WmLButtonUp)
            {
                LeftClicked?.Invoke();
                return IntPtr.Zero;
            }
            if (eventId == WmContextMenu)
            {
                if (GetCursorPos(out var pt))
                    RightClicked?.Invoke(pt.X, pt.Y);
                return IntPtr.Zero;
            }
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private static IntPtr LoadIconFromStream(Stream stream)
    {
        // Try extracting from the executable first as fallback
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        // Parse ICO header: 6 bytes header + 16 bytes per entry
        if (bytes.Length >= 22 && BitConverter.ToUInt16(bytes, 0) == 0 && BitConverter.ToUInt16(bytes, 2) == 1)
        {
            var count = BitConverter.ToUInt16(bytes, 4);
            var bestOffset = 0;
            var bestSize = 0;
            var bestScore = -1;
            for (int i = 0; i < count && 6 + (i + 1) * 16 <= bytes.Length; i++)
            {
                int entryOff = 6 + i * 16;
                int w = bytes[entryOff] == 0 ? 256 : bytes[entryOff];
                int size = BitConverter.ToInt32(bytes, entryOff + 8);
                int offset = BitConverter.ToInt32(bytes, entryOff + 12);
                int score = w == 32 ? 1000 : (w == 48 ? 900 : (w == 16 ? 800 : w));
                if (score > bestScore && offset > 0 && offset + size <= bytes.Length)
                {
                    bestScore = score;
                    bestOffset = offset;
                    bestSize = size;
                }
            }

            if (bestSize > 0)
            {
                var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try
                {
                    var ptr = IntPtr.Add(pinned.AddrOfPinnedObject(), bestOffset);
                    var hIcon = CreateIconFromResourceEx(ptr, (uint)bestSize, true, 0x00030000, 32, 32, 0);
                    if (hIcon != IntPtr.Zero) return hIcon;
                }
                finally
                {
                    pinned.Free();
                }
            }
        }

        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            var icons = new IntPtr[1];
            if (ExtractIconExW(exePath, 0, IntPtr.Zero, icons, 1) > 0 && icons[0] != IntPtr.Zero)
                return icons[0];
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hwnd != IntPtr.Zero)
        {
            var data = CreateNotifyIconData(0);
            Shell_NotifyIconW(NimDelete, ref data);
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
        UnregisterClassW(_className, GetModuleHandleW(null));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconExW(string lpszFile, int nIconIndex, IntPtr phiconLarge, IntPtr[] phiconSmall, uint nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconFromResourceEx(IntPtr presbits, uint dwResSize, bool fIcon, uint dwVer, int cxDesired, int cyDesired, uint Flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessageW(string lpString);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClassW(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);
}
