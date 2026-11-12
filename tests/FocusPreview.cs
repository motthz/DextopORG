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
        var source = new Form { Text = "DextopORG test source", StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(80,80,600,400) };
        source.Paint += delegate(object sender, PaintEventArgs e) {
            e.Graphics.FillRectangle(Brushes.DarkBlue, 0,0,600,400);
            e.Graphics.FillRectangle(Brushes.DarkGreen,100,100,200,100);
            e.Graphics.DrawString("LIVE ZONE", new Font("Arial",18), Brushes.White,110,130);
        };
        var button = new Button { Text = "Select region", Bounds = new Rectangle(10,10,120,30) };
        source.Controls.Add(button);
        button.Click += delegate {
            Native.Rect rect; Native.GetWindowRect(source.Handle,out rect);
            var bounds = Rectangle.FromLTRB(rect.left,rect.top,rect.right,rect.bottom);
            using (var selector = new RegionSelector(bounds)) {
                if (selector.ShowDialog() != DialogResult.OK) return;
                var view = new FocusView(source.Handle,selector.Selection,new Rectangle(700,80,600,500));
                view.Show();
            }
        };
        Application.Run(source);
    }
}
