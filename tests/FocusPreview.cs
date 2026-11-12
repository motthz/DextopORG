using System;
using System.Drawing;
using System.Windows.Forms;

// Manual Windows fixture. Never starts the organizer or moves other applications.
class FocusPreview
{
    [STAThread] static void Main()
    {
        Native.SetProcessDPIAware();
        Application.EnableVisualStyles();
        var source = new Form { Text = "DextopORG test source", StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(80,80,600,400), FormBorderStyle = FormBorderStyle.FixedSingle };
        WindowCrop crop = null;
        var second = new Form { Text = "DextopORG minimum-size test", StartPosition = FormStartPosition.Manual, MinimumSize = new Size(900,700), Bounds = new Rectangle(800,80,900,700), BackColor = Color.DarkRed, FormBorderStyle = FormBorderStyle.FixedSingle };
        WindowCrop secondCrop = null;
        source.Shown += delegate {
            second.Show();
            secondCrop = new WindowCrop(second.Handle);
            secondCrop.Apply(new Rectangle(800,80,300,400));
        };
        source.Paint += delegate(object sender, PaintEventArgs e) {
            e.Graphics.FillRectangle(Brushes.DarkBlue, 0,0,600,400);
            e.Graphics.FillRectangle(Brushes.DarkGreen,100,100,200,100);
            using (var font = new Font("Arial",18)) e.Graphics.DrawString("ORIGINAL", font, Brushes.White,110,130);
        };
        var button = new Button { Text = "Select region", Bounds = new Rectangle(10,10,120,30) };
        source.Controls.Add(button);
        var interactive = new Button { Text = "Click originale", Bounds = new Rectangle(110,160,150,28) };
        source.Controls.Add(interactive);
        interactive.Click += delegate { interactive.Text = "Click ricevuto"; };
        source.KeyPreview = true;
        source.KeyDown += delegate(object sender, KeyEventArgs e) { if(e.KeyCode==Keys.Escape && crop!=null) { crop.Dispose(); crop=null; } };
        button.Click += delegate {
            Native.Rect rect; Native.GetWindowRect(source.Handle,out rect);
            var bounds = Rectangle.FromLTRB(rect.left,rect.top,rect.right,rect.bottom);
            using (var selector = new RegionSelector(bounds)) {
                if (selector.ShowDialog() != DialogResult.OK) return;
                if (crop != null) crop.Dispose();
                crop = new WindowCrop(source.Handle) { Selection = selector.Selection };
                crop.Apply(new Rectangle(80,80,600,400));
            }
        };
        source.FormClosed += delegate { if(crop!=null)crop.Dispose(); if(secondCrop!=null)secondCrop.Dispose(); second.Close(); };
        Application.Run(source);
    }
}
