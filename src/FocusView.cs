using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class FocusGeometry
{
    public static Rectangle Fit(Size source, Rectangle target)
    {
        if (source.Width <= 0 || source.Height <= 0 || target.Width <= 0 || target.Height <= 0) return Rectangle.Empty;
        double scale = Math.Min((double)target.Width / source.Width, (double)target.Height / source.Height);
        int w = Math.Max(1, (int)(source.Width * scale));
        int h = Math.Max(1, (int)(source.Height * scale));
        return new Rectangle(target.X + (target.Width - w) / 2, target.Y + (target.Height - h) / 2, w, h);
    }
}

internal sealed class RegionSelector : Form
{
    Point anchor;
    bool dragging;
    public Rectangle Selection;
    public RegionSelector(Rectangle bounds)
    {
        Text = "DextopORG selezione zona";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        Opacity = 0.35;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;
    }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } base.OnKeyDown(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) { DialogResult = DialogResult.Cancel; Close(); return; }
        anchor = e.Location; dragging = true; Capture = true;
        base.OnMouseDown(e);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging) {
            int x = Math.Max(0, Math.Min(ClientSize.Width, e.X));
            int y = Math.Max(0, Math.Min(ClientSize.Height, e.Y));
            Selection = Rectangle.FromLTRB(Math.Min(anchor.X,x), Math.Min(anchor.Y,y), Math.Max(anchor.X,x), Math.Max(anchor.Y,y));
            Invalidate();
        }
        base.OnMouseMove(e);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!dragging) return;
        OnMouseMove(e); dragging = false; Capture = false;
        if (Selection.Width >= 8 && Selection.Height >= 8) { DialogResult = DialogResult.OK; Close(); }
        base.OnMouseUp(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawString("Trascina sulla zona da ingrandire - Esc annulla", SystemFonts.MessageBoxFont, Brushes.White, 12, 12);
        if (Selection.Width > 0 && Selection.Height > 0)
            using (var pen = new Pen(Color.Lime, 3)) e.Graphics.DrawRectangle(pen, Selection);
    }
}

internal sealed class FocusView : Form
{
    public readonly IntPtr Source;
    readonly Rectangle region;
    IntPtr thumbnail;
    readonly Timer watch = new Timer { Interval = 300 };
    public FocusView(IntPtr source, Rectangle selected, Rectangle bounds)
    {
        Source = source; region = selected;
        Text = "DextopORG Zoom - vista live - Esc chiude";
        BackColor = Color.FromArgb(20,20,24);
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        MinimumSize = new Size(160,120);
        TopMost = true;
        KeyPreview = true;
        watch.Tick += delegate {
            if (!Native.IsWindow(Source)) { Close(); return; }
            Text = Native.IsIconic(Source) ? "DextopORG Zoom - ripristina la finestra originale" : "DextopORG Zoom - vista live - Esc chiude";
            UpdateThumbnail();
        };
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        int result = DwmRegisterThumbnail(Handle, Source, out thumbnail);
        if (result != 0) { Close(); Marshal.ThrowExceptionForHR(result); }
        UpdateThumbnail(); watch.Start();
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateThumbnail(); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); base.OnKeyDown(e); }
    void UpdateThumbnail()
    {
        if (thumbnail == IntPtr.Zero) return;
        var dest = FocusGeometry.Fit(region.Size, ClientRectangle);
        var p = new ThumbnailProperties {
            flags = 31, destination = ToRect(dest), source = ToRect(region), opacity = 255,
            visible = !Native.IsIconic(Source), clientOnly = false
        };
        int result = DwmUpdateThumbnailProperties(thumbnail, ref p);
        if (result != 0) { Text = "DextopORG Zoom - contenuto non disponibile"; }
    }
    static Native.Rect ToRect(Rectangle r) { return new Native.Rect { left=r.Left, top=r.Top, right=r.Right, bottom=r.Bottom }; }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { watch.Stop(); watch.Dispose(); }
        if (thumbnail != IntPtr.Zero) { DwmUnregisterThumbnail(thumbnail); thumbnail = IntPtr.Zero; }
        base.Dispose(disposing);
    }
    [StructLayout(LayoutKind.Sequential)] struct ThumbnailProperties {
        public uint flags;
        public Native.Rect destination, source;
        public byte opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool visible;
        [MarshalAs(UnmanagedType.Bool)] public bool clientOnly;
    }
    [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr destination, IntPtr source, out IntPtr thumbnail);
    [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, ref ThumbnailProperties properties);
    [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
}
