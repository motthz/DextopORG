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
    bool selecting;
    readonly Dictionary<IntPtr, WindowCrop> crops = new Dictionary<IntPtr, WindowCrop>();
    bool warned;

    public Organizer()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Riordina ora   Ctrl+Alt+O", null, delegate { Arrange(); });
        pause = new ToolStripMenuItem("Pausa   Ctrl+Alt+P", null, delegate { Toggle(); });
        menu.Items.Add(pause);
        menu.Items.Add("Ripristina e metti in pausa   Ctrl+Alt+R", null, delegate { Restore(); });
        menu.Items.Add("Ritaglia finestra: istruzioni   Ctrl+Alt+Z", null, delegate {
            tray.ShowBalloonTip(6000, "Seleziona una zona", "Mouse sulla finestra, Ctrl+Alt+Z e trascina. Stessa combinazione per togliere il ritaglio. Nessuna finestra duplicata.", ToolTipIcon.Info);
        });
        menu.Items.Add("Rimuovi tutti i ritagli selezionati", null, delegate { ClearFocus(); if (enabled) Arrange(); });
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
        hotkeys = new HotkeyWindow(id => { if (id == 1) Arrange(); if (id == 2) Toggle(); if (id == 3) Restore(); if (id == 4) SelectFocus(); });
        if (!hotkeys.AllRegistered) tray.ShowBalloonTip(6000, "DextopORG", "Una scorciatoia è già in uso. Usa il menu dell'icona.", ToolTipIcon.Warning);
        timer.Tick += delegate {
            if (!enabled || restoring || selecting || Native.GetAsyncKeyState(1) < 0) return;
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
            uint pid; Native.GetWindowThreadProcessId(h, out pid);
            if (pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id) return true;
            long style = Native.GetWindowLong(h, -16), ex = Native.GetWindowLong(h, -20);
            if ((style & (0x40000 | 0xC00000)) == 0 || (ex & 0x80) != 0) return true;
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
        return string.Join(";", windows.Select(h => h.ToInt64() + "@" + ScreenFor(h).DeviceName + WindowCrop.Bounds(h) + WindowCrop.VisibleBounds(h))) + "|" +
            string.Join(";", Screen.AllScreens.Select(s => s.DeviceName + s.WorkingArea.ToString()));
    }
    void Toggle()
    {
        enabled = !enabled;
        pause.Checked = !enabled;
        tray.Text = enabled ? "DextopORG · automatico" : "DextopORG · in pausa";
        if (!enabled) { ClearFocus(); ReleaseCrops(); }
        if (enabled) Arrange();
    }
    Screen ScreenFor(IntPtr h) { return Screen.FromRectangle(WindowCrop.VisibleBounds(h)); }
    void SaveOriginal(IntPtr h)
    {
        if (originals.ContainsKey(h)) return;
        var p = new Native.Placement { length = Marshal.SizeOf(typeof(Native.Placement)) };
        if (Native.GetWindowPlacement(h, ref p)) originals[h] = p;
    }
    WindowCrop CropFor(IntPtr h)
    {
        WindowCrop crop;
        if (!crops.TryGetValue(h,out crop)) { crop = new WindowCrop(h); crops.Add(h,crop); }
        return crop;
    }
    void Arrange() { Arrange(Windows()); }
    void Arrange(List<IntPtr> windows)
    {
        foreach (var dead in originals.Keys.Where(h => !Native.IsWindow(h)).ToArray()) originals.Remove(dead);
        foreach (var dead in crops.Where(p => !p.Value.IsCurrent).Select(p => p.Key).ToArray()) { crops[dead].Dispose(); crops.Remove(dead); originals.Remove(dead); }
        bool failed = false;
        foreach (var group in windows.GroupBy(h => ScreenFor(h).DeviceName))
        {
            var handles = group.ToArray();
            var screen = ScreenFor(handles[0]);
            var tiles = Layout.Compute(screen.WorkingArea, handles.Length);
            for (int i = 0; i < handles.Length; i++)
            {
                IntPtr h = handles[i];
                SaveOriginal(h);
                var crop = CropFor(h);
                if (crop.Selection.HasValue) { if (!crop.Apply(tiles[i])) failed = true; continue; }
                if (Native.IsZoomed(h)) Native.ShowWindow(h, 4);
                Rectangle t = tiles[i];
                // Use outer bounds directly. DWM frame extents can change after a
                // region is applied; feeding them back here would cause size drift.
                if (!Native.SetWindowPos(h, IntPtr.Zero, t.X, t.Y, t.Width, t.Height, 0x0014 | 0x0200) || !crop.Apply(tiles[i])) failed = true;
            }
        }
        signature = Signature(windows);
        if (failed && !warned) {
            tray.ShowBalloonTip(6000,"Finestra non controllabile", "Una finestra ha rifiutato posizione o ritaglio (per esempio per privilegi diversi). Su quella finestra non posso garantire l'assenza di sovrapposizioni.",ToolTipIcon.Warning);
            warned = true;
        }
        if (!failed) warned = false;
    }
    void Restore()
    {
        if (enabled) Toggle();
        ClearFocus(); ReleaseCrops();
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
        ClearFocus(); ReleaseCrops();
        timer.Stop(); timer.Dispose(); hotkeys.Dispose();
        tray.Visible = false; tray.Dispose();
        base.ExitThreadCore();
    }

    void ClearFocus()
    {
        foreach (var pair in crops.Where(p => p.Value.Selection.HasValue).ToArray()) {
            pair.Value.Restore(); pair.Value.Selection = null;
            Native.Placement p;
            if (pair.Value.IsCurrent && originals.TryGetValue(pair.Key,out p)) Native.SetWindowPlacement(pair.Key,ref p);
        }
        signature = "";
    }
    void ReleaseCrops() { foreach (var crop in crops.Values) crop.Dispose(); crops.Clear(); }
    void SelectFocus()
    {
        if (selecting) return;
        IntPtr source = Native.GetAncestor(Native.WindowFromPoint(new Native.Point { x = Cursor.Position.X, y = Cursor.Position.Y }), 2);
        if (source == IntPtr.Zero) return;
        uint pid;
        Native.GetWindowThreadProcessId(source, out pid);
        if (pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id) return;
        if (!Windows().Contains(source)) return;
        WindowCrop existing;
        if (crops.TryGetValue(source,out existing) && existing.Selection.HasValue) {
            existing.Selection = null; existing.Restore();
            Native.Placement p; if (originals.TryGetValue(source,out p)) Native.SetWindowPlacement(source,ref p);
            if (enabled) Arrange();
            return;
        }
        SaveOriginal(source);
        if (Native.IsZoomed(source)) Native.ShowWindow(source,4);
        Native.Rect rect;
        if (!Native.IsWindowVisible(source) || Native.IsIconic(source) || !Native.GetWindowRect(source, out rect)) return;
        Rectangle bounds = Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);
        Rectangle visible = Rectangle.Intersect(WindowCrop.VisibleBounds(source), ScreenFor(source).Bounds);
        if (visible.Width < 16 || visible.Height < 16) return;
        selecting = true;
        try {
            using (var selector = new RegionSelector(visible)) {
                if (selector.ShowDialog() != DialogResult.OK || !Native.IsWindow(source)) return;
                Rectangle selected = selector.Selection;
                selected.Offset(visible.Left - bounds.Left, visible.Top - bounds.Top);
                var crop = CropFor(source);
                crop.Selection = selected;
                // Arrange once even when paused: the chosen area must never cover another tile.
                Arrange();
                signature = "";
            }
        } catch (Exception e) {
            tray.ShowBalloonTip(5000, "Ritaglio non disponibile", e.Message, ToolTipIcon.Warning);
        } finally { selecting = false; }
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
        int[] keys = { (int)Keys.O, (int)Keys.P, (int)Keys.R, (int)Keys.Z };
        for (int i = 0; i < keys.Length; i++)
            if (!Native.RegisterHotKey(Handle, i + 1, 0x4003, keys[i])) AllRegistered = false;
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312) action(m.WParam.ToInt32());
        base.WndProc(ref m);
    }
    public void Dispose() { for (int i = 1; i <= 4; i++) Native.UnregisterHotKey(Handle, i); DestroyHandle(); }
}

internal static class Native
{
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] public struct Placement { public int length, flags, showCmd; public Point min, max; public Rect normal; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point p);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
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
