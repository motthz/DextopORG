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
        int crops = 0;
        foreach (var source in new[] { new Rectangle(40,70,400,100), new Rectangle(15,30,100,400), new Rectangle(20,20,133,177), new Rectangle(10,30,800,600) })
        foreach (var target in new[] { new Rectangle(0,0,1920,1080), new Rectangle(13,27,333,777), new Rectangle(0,0,1,1) }) {
            var origin = CropGeometry.WindowOrigin(source,target);
            var window = new Rectangle(origin,new Size(source.Right+30,source.Bottom+50));
            var local = CropGeometry.LocalClip(window,target,source);
            var visible = local; visible.Offset(origin);
            if (!target.Contains(visible)) throw new Exception("Crop bounds");
            if (!source.Contains(local)) throw new Exception("Crop outside selection");
            if (Math.Abs((visible.Left * 2 + visible.Width) - (target.Left * 2 + target.Width)) > 1 ||
                Math.Abs((visible.Top * 2 + visible.Height) - (target.Top * 2 + target.Height)) > 1) throw new Exception("Crop centering");
            if (visible.Width != Math.Min(source.Width,target.Width) || visible.Height != Math.Min(source.Height,target.Height)) throw new Exception("Crop native size");
            crops++;
        }
        foreach (var area in new[] { new Rectangle(-1000,50,1920,1040), new Rectangle(7,13,1365,767) })
        for (int n = 1; n <= 20; n++) {
            var tiles = Layout.Compute(area,n);
            for (int i=0;i<n;i++) {
                // Simulate apps refusing to shrink below 1600x1000.
                var window = new Rectangle(tiles[i].Location,new Size(1600,1000));
                var local = CropGeometry.LocalClip(window,tiles[i],null);
                local.Offset(window.Location);
                if (!tiles[i].Contains(local)) throw new Exception("Minimum size overflow");
                for (int j=0;j<n;j++) if(i!=j && tiles[j].IntersectsWith(local)) throw new Exception("Clipped overlap");
            }
        }
        if (CropGeometry.LocalClip(new Rectangle(0,0,10,10),new Rectangle(50,50,10,10),null) != Rectangle.Empty) throw new Exception("Empty clip");
        Console.WriteLine("PASS: " + checks + " layouts; " + crops + " crop centering cases; 40 oversized-window layouts; empty inputs.");
    }
}
