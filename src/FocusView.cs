using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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
        e.Graphics.DrawString("Trascina sulla zona da mantenere visibile - Esc annulla", SystemFonts.MessageBoxFont, Brushes.White, 12, 12);
        if (Selection.Width > 0 && Selection.Height > 0)
            using (var pen = new Pen(Color.Lime, 3)) e.Graphics.DrawRectangle(pen, Selection);
    }
}

internal static class CropGeometry
{
    // Keep the selected pixels at their native size; do not resize/reflow the source app.
    public static Point WindowOrigin(Rectangle selected, Rectangle tile)
    {
        return new Point(tile.Left + (tile.Width - selected.Width) / 2 - selected.Left,
            tile.Top + (tile.Height - selected.Height) / 2 - selected.Top);
    }
    public static Rectangle LocalClip(Rectangle window, Rectangle tile, Rectangle? selected)
    {
        var visible = Rectangle.Intersect(window, tile);
        if (selected.HasValue) {
            var area = selected.Value;
            area.Offset(window.Location);
            visible = Rectangle.Intersect(visible, area);
        }
        if (visible.Width <= 0 || visible.Height <= 0) return Rectangle.Empty;
        visible.Offset(-window.Left, -window.Top);
        return visible;
    }
}

// Owns a saved copy of the app's original window region. Never owns the live HRGN
// after SetWindowRgn succeeds: Windows takes ownership of that handle.
internal sealed class WindowCrop : IDisposable
{
    public readonly IntPtr Handle;
    readonly uint processId;
    IntPtr original;
    bool hadRegion;
    bool applied;
    public Rectangle? Selection;
    public WindowCrop(IntPtr handle)
    {
        Handle = handle;
        Native.GetWindowThreadProcessId(handle, out processId);
        original = CreateRectRgn(0,0,0,0);
        if (original == IntPtr.Zero) throw new InvalidOperationException("Impossibile salvare il ritaglio originale.");
        hadRegion = GetWindowRgn(handle, original) != 0;
    }
    public bool IsCurrent {
        get { uint pid; Native.GetWindowThreadProcessId(Handle, out pid); return Native.IsWindow(Handle) && pid == processId; }
    }
    public static Rectangle Bounds(IntPtr handle)
    {
        Native.Rect r;
        return Native.GetWindowRect(handle, out r) ? Rectangle.FromLTRB(r.left,r.top,r.right,r.bottom) : Rectangle.Empty;
    }
    public static Rectangle VisibleBounds(IntPtr handle)
    {
        var bounds = Bounds(handle);
        Native.Rect r;
        if (GetWindowRgnBox(handle, out r) > 0) {
            var region = Rectangle.FromLTRB(r.left,r.top,r.right,r.bottom);
            region.Offset(bounds.Location);
            bounds = Rectangle.Intersect(bounds,region);
        }
        return bounds;
    }
    public bool Apply(Rectangle tile)
    {
        if (!IsCurrent) return false;
        if (Selection.HasValue) {
            var origin = CropGeometry.WindowOrigin(Selection.Value,tile);
            if (!Native.SetWindowPos(Handle,IntPtr.Zero,origin.X,origin.Y,0,0,0x0015 | 0x0200)) return false;
        }
        var clip = CropGeometry.LocalClip(Bounds(Handle),tile,Selection);
        IntPtr region = CreateRectRgn(clip.Left,clip.Top,clip.Right,clip.Bottom);
        if (region == IntPtr.Zero) return false;
        if (hadRegion && CombineRgn(region,region,original,1) == 0) { DeleteObject(region); return false; }
        if (SetWindowRgn(Handle,region,true) == 0) { DeleteObject(region); return false; }
        applied = true;
        return true;
    }
    public bool Restore()
    {
        if (!applied || !IsCurrent) return true;
        IntPtr region = IntPtr.Zero;
        if (hadRegion) {
            region = CreateRectRgn(0,0,0,0);
            if (region == IntPtr.Zero) return false;
            if (CombineRgn(region,original,IntPtr.Zero,5) == 0) { DeleteObject(region); return false; }
        }
        if (SetWindowRgn(Handle,region,true) == 0) { if (region != IntPtr.Zero) DeleteObject(region); return false; }
        applied = false;
        return true;
    }
    public void Dispose()
    {
        Restore();
        if (original != IntPtr.Zero) { DeleteObject(original); original = IntPtr.Zero; }
    }
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
    [DllImport("gdi32.dll")] static extern int CombineRgn(IntPtr dest,IntPtr a,IntPtr b,int mode);
    [DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr h,IntPtr region);
    [DllImport("user32.dll")] static extern int GetWindowRgnBox(IntPtr h,out Native.Rect rect);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h,IntPtr region,bool redraw);
}
