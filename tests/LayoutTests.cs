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
        Console.WriteLine("PASS: " + checks + " layouts, count/bounds/overlap/full coverage; empty layout.");
    }
}
