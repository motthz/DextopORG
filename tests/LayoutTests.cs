using System;
using System.Drawing;
class LayoutTests
{
    static void Main()
    {
        int checks = 0;
        foreach (var area in new[] { new Rectangle(0,0,1920,1040), new Rectangle(-2560,0,2560,1400), new Rectangle(0,-1920,1080,1880), new Rectangle(7,13,1365,767) })
        for (int n = 1; n <= 50; n++) {
            var tiles = Layout.Compute(area,n);
            if (tiles.Length != n) throw new Exception("Count");
            long pixels = 0;
            for (int i = 0; i < n; i++) {
                if (!area.Contains(tiles[i]) || tiles[i].Width <= 0 || tiles[i].Height <= 0) throw new Exception("Bounds");
                pixels += (long)tiles[i].Width * tiles[i].Height;
                for (int j = 0; j < i; j++) if (tiles[i].IntersectsWith(tiles[j])) throw new Exception("Overlap");
            }
            if (pixels != (long)area.Width * area.Height) throw new Exception("Unused space");
            checks++;
        }
        if (Layout.Compute(new Rectangle(0,0,100,100),0).Length != 0) throw new Exception("Empty");
        foreach (var source in new[] { new Size(400,100), new Size(100,400), new Size(133,177), new Size(800,600) })
        foreach (var target in new[] { new Rectangle(0,0,1920,1080), new Rectangle(13,27,333,777), new Rectangle(0,0,1,1) }) {
            var fit = FocusGeometry.Fit(source,target);
            if (!target.Contains(fit)) throw new Exception("Zoom bounds");
            if (Math.Abs((fit.Left * 2 + fit.Width) - (target.Left * 2 + target.Width)) > 1 ||
                Math.Abs((fit.Top * 2 + fit.Height) - (target.Top * 2 + target.Height)) > 1) throw new Exception("Zoom centering");
            if (fit.Width > 1 && fit.Height > 1 && Math.Abs((double)fit.Width / fit.Height - (double)source.Width / source.Height) > 0.05) throw new Exception("Zoom aspect");
        }
        if (FocusGeometry.Fit(Size.Empty,new Rectangle(0,0,10,10)) != Rectangle.Empty) throw new Exception("Empty zoom");
        Console.WriteLine("PASS: " + checks + " layouts; 12 zoom aspect/bounds/centering cases; empty inputs.");
    }
}
