using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Program
{
    [STAThread] static void Main()
    {
        bool created;
        using (var mutex = new System.Threading.Mutex(true, "Local\\DextopORG", out created))
        {
            if (!created) return;
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.Run(new Organizer());
        }
    }
}

// Searches balanced row layouts; every pixel belongs to exactly one tile.
internal static class Layout
{
    public static Rectangle[] Compute(Rectangle area, int count)
    {
        if (count <= 0) return new Rectangle[0];
        double best = double.MaxValue;
        Rectangle[] winner = null;
        for (int rows = 1; rows <= count; rows++)
        {
            var tiles = new List<Rectangle>();
            double score = 0;
            for (int r = 0; r < rows; r++)
            {
                int cols = count / rows + (r < count % rows ? 1 : 0);
                int top = area.Top + area.Height * r / rows;
                int bottom = area.Top + area.Height * (r + 1) / rows;
                for (int c = 0; c < cols; c++)
                {
                    int left = area.Left + area.Width * c / cols;
                    int right = area.Left + area.Width * (c + 1) / cols;
                    var tile = Rectangle.FromLTRB(left, top, right, bottom);
                    double ratio = (double)Math.Max(1, tile.Width) / Math.Max(1, tile.Height);
                    score += Math.Pow(Math.Log(ratio / 1.6), 2);
                    tiles.Add(tile);
                }
            }
            if (score < best) { best = score; winner = tiles.ToArray(); }
        }
        return winner;
    }
}

internal sealed class Organizer : ApplicationContext
{
    readonly NotifyIcon tray;
    readonly HotkeyWindow hotkeys;
    readonly Timer timer = new Timer { Interval = 800 };
    readonly Dictionary<IntPtr, Native.Placement> originals = new Dictionary<IntPtr, Native.Placement>();
    readonly ToolStripMenuItem pause;
    string signature = "";
    bool enabled = true;
    bool restoring;

    public Organizer()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Riordina ora   Ctrl+Alt+O", null, delegate { Arrange(); });
        pause = new ToolStripMenuItem("Pausa   Ctrl+Alt+P", null, delegate { Toggle(); });
        menu.Items.Add(pause);
        menu.Items.Add("Ripristina e metti in pausa   Ctrl+Alt+R", null, delegate { Restore(); });
        var startup = new ToolStripMenuItem("Avvia con Windows");
        using (var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
            startup.Checked = key != null && key.GetValue("DextopORG") != null;
        startup.Click += delegate {
            try {
                using (var key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")) {
                    if (startup.Checked) key.DeleteValue("DextopORG", false);
                    else key.SetValue("DextopORG", "\"" + Application.ExecutablePath + "\"");
                }
                startup.Checked = !startup.Checked;
            } catch (Exception e) { MessageBox.Show(e.Message, "Avvio automatico"); }
        };
        menu.Items.Add(startup);
        menu.Items.Add("Esci", null, delegate { ExitThread(); });
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "DextopORG · automatico", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += delegate { Toggle(); };
        hotkeys = new HotkeyWindow(id => { if (id == 1) Arrange(); if (id == 2) Toggle(); if (id == 3) Restore(); });
        if (!hotkeys.AllRegistered) tray.ShowBalloonTip(6000, "DextopORG", "Una scorciatoia è già in uso. Usa il menu dell'icona.", ToolTipIcon.Warning);
        timer.Tick += delegate {
            if (!enabled || restoring || Native.GetAsyncKeyState(1) < 0) return;
            var windows = Windows();
            string next = Signature(windows);
            if (next != signature) Arrange(windows);
        };
        timer.Start();
        tray.ShowBalloonTip(4000, "DextopORG attivo", "Ctrl+Alt+P: pausa · Ctrl+Alt+R: ripristina. Menu sull'icona per le opzioni.", ToolTipIcon.Info);
    }

    List<IntPtr> Windows()
    {
        var result = new List<IntPtr>();
        Native.EnumWindows(delegate(IntPtr h, IntPtr p) {
            if (!Native.IsWindowVisible(h) || Native.IsIconic(h) || Native.GetWindow(h, 4) != IntPtr.Zero) return true;
            long style = Native.GetWindowLong(h, -16), ex = Native.GetWindowLong(h, -20);
            if ((style & 0x40000) == 0 || (ex & 0x80) != 0) return true;
            int cloaked;
            if (Native.DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
            var name = new StringBuilder(256);
            Native.GetClassName(h, name, name.Capacity);
            if (name.ToString() == "Shell_TrayWnd" || name.ToString() == "Progman" || name.ToString() == "WorkerW") return true;
            result.Add(h);
            return true;
        }, IntPtr.Zero);
        return result.OrderBy(h => h.ToInt64()).ToList();
    }

    string Signature(List<IntPtr> windows)
    {
        return string.Join(";", windows.Select(h => h.ToInt64() + "@" + Screen.FromHandle(h).DeviceName)) + "|" +
            string.Join(";", Screen.AllScreens.Select(s => s.DeviceName + s.WorkingArea.ToString()));
    }
    void Toggle()
    {
        enabled = !enabled;
        pause.Checked = !enabled;
        tray.Text = enabled ? "DextopORG · automatico" : "DextopORG · in pausa";
        if (enabled) Arrange();
    }
    void Arrange() { Arrange(Windows()); }
    void Arrange(List<IntPtr> windows)
    {
        foreach (var dead in originals.Keys.Where(h => !Native.IsWindow(h)).ToArray()) originals.Remove(dead);
        foreach (var group in windows.GroupBy(h => Screen.FromHandle(h).DeviceName))
        {
            var handles = group.ToArray();
            var screen = Screen.FromHandle(handles[0]);
            var tiles = Layout.Compute(screen.WorkingArea, handles.Length);
            for (int i = 0; i < handles.Length; i++)
            {
                IntPtr h = handles[i];
                if (!originals.ContainsKey(h)) {
                    var placement = new Native.Placement { length = Marshal.SizeOf(typeof(Native.Placement)) };
                    if (Native.GetWindowPlacement(h, ref placement)) originals[h] = placement;
                }
                if (Native.IsZoomed(h)) Native.ShowWindow(h, 4);
                Rectangle t = tiles[i];
                // Account for invisible resize borders so visible frames meet exactly.
                Native.Rect outer, frame;
                if (Native.GetWindowRect(h, out outer) && Native.DwmGetWindowAttribute(h, 9, out frame, Marshal.SizeOf(typeof(Native.Rect))) == 0) {
                    t = Rectangle.FromLTRB(t.Left - (frame.left - outer.left), t.Top - (frame.top - outer.top),
                        t.Right + (outer.right - frame.right), t.Bottom + (outer.bottom - frame.bottom));
                }
                Native.SetWindowPos(h, IntPtr.Zero, t.X, t.Y, t.Width, t.Height, 0x0014 | 0x0200);
            }
        }
        signature = Signature(windows);
    }
    void Restore()
    {
        if (enabled) Toggle();
        restoring = true;
        foreach (var pair in originals) {
            var p = pair.Value;
            if (Native.IsWindow(pair.Key)) Native.SetWindowPlacement(pair.Key, ref p);
        }
        originals.Clear();
        restoring = false;
    }
    protected override void ExitThreadCore()
    {
        timer.Stop(); timer.Dispose(); hotkeys.Dispose();
        tray.Visible = false; tray.Dispose();
        base.ExitThreadCore();
    }
}

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    readonly Action<int> action;
    public bool AllRegistered = true;
    public HotkeyWindow(Action<int> handler)
    {
        action = handler;
        CreateHandle(new CreateParams());
        int[] keys = { (int)Keys.O, (int)Keys.P, (int)Keys.R };
        for (int i = 0; i < keys.Length; i++)
            if (!Native.RegisterHotKey(Handle, i + 1, 0x4003, keys[i])) AllRegistered = false;
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312) action(m.WParam.ToInt32());
        base.WndProc(ref m);
    }
    public void Dispose() { for (int i = 1; i <= 3; i++) Native.UnregisterHotKey(Handle, i); DestroyHandle(); }
}

internal static class Native
{
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] public struct Placement { public int length, flags, showCmd; public Point min, max; public Rect normal; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr h, ref Placement p);
    [DllImport("user32.dll")] public static extern bool SetWindowPlacement(IntPtr h, ref Placement p);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, int key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attribute, out Rect value, int size);
}
