using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class CropNativeTests
{
    [STAThread] static void Main()
    {
        Native.SetProcessDPIAware();
        using (var form = new Form { StartPosition=FormStartPosition.Manual, Bounds=new Rectangle(100,100,900,700), MinimumSize=new Size(900,700) }) {
            IntPtr handle = form.Handle;
            int formCount = Application.OpenForms.Count;
            var originalBounds = WindowCrop.Bounds(handle);
            var tile = new Rectangle(100,100,300,200);
            using (var crop = new WindowCrop(handle)) {
                if (!crop.Apply(tile)) throw new Exception("Native crop failed");
                if (!tile.Contains(WindowCrop.VisibleBounds(handle))) throw new Exception("Minimum window overflow");
                if (!crop.Restore()) throw new Exception("Native restore failed");
                if (WindowCrop.VisibleBounds(handle) != originalBounds) throw new Exception("Full region was not restored");
                crop.Selection = new Rectangle(100,100,200,80);
                if (!crop.Apply(tile)) throw new Exception("Focus crop failed");
                var visible = WindowCrop.VisibleBounds(handle);
                if (visible != new Rectangle(150,160,200,80)) throw new Exception("Native centering: " + visible);
                if (form.Handle != handle || Application.OpenForms.Count != formCount) throw new Exception("Window was replaced");
            }
            IntPtr custom = CreateRoundRectRgn(0,0,400,300,20,20);
            if (SetWindowRgn(handle,custom,true) == 0) throw new Exception("Fixture region failed");
            var expected = WindowCrop.VisibleBounds(handle);
            using (var crop = new WindowCrop(handle)) {
                if (!crop.Apply(new Rectangle(WindowCrop.Bounds(handle).Location,new Size(100,100)))) throw new Exception("Custom region crop");
            }
            if (WindowCrop.VisibleBounds(handle) != expected) throw new Exception("Original custom region lost");
        }
        Console.WriteLine("PASS: native minimum-size confinement, same HWND, focus centering, region restoration, original custom region preservation.");
    }
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l,int t,int r,int b,int w,int h);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h,IntPtr region,bool redraw);
}
