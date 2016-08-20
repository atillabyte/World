using System;
using System.Collections.Generic;
using System.Drawing;
using PlayerIOClient;
using System.Net;
using System.Linq;

internal class Program
{
    static void Main(string[] args)
    {
        var client = PlayerIO.QuickConnect.SimpleConnect("everybody-edits-su9rn58o40itdbnw69plyw", "guest", "guest", null);
        var world = new World(World.WorldType.JSON, client, @"PWInputAn_IdEI.json");

        var bitmap = GenerateMinimap(world);

        bitmap.Save(@"PWInputAn_IdEI.png");

        Console.WriteLine("Done.");
        Console.ReadLine();
    }

    public static Bitmap GenerateMinimap(dynamic world)
    {
        var canvas = new Bitmap((int?)world.width ?? 200, (int?)world.height ?? 200);
        var bitmap = new FastBitmap(canvas);

        var bgcolor = world["backgroundColor"];

        bitmap.Lock();
        bitmap.Clear((bgcolor == null) ? -16777216 : Color.FromArgb(255, (byte)((uint)bgcolor >> 16), (byte)((uint)bgcolor >> 8), (byte)((uint)bgcolor >> 0)).ToArgb());

        foreach (dynamic block in ((IEnumerable<dynamic>)world.Blocks).Where(block => colors[block.type] != 0u).OrderByDescending(x => x.type >= 500))
            foreach (var position in block.Positions)
                bitmap.SetPixel((int)position.X, (int)position.Y, colors[block.type]);

        bitmap.Unlock();
        return canvas;
    }

    public static Dictionary<int, uint> colors = new WebClient() { Proxy = null }.DownloadString("https://raw.githubusercontent.com/EEJesse/EEBlocks/master/Colors.txt").Split('\n')
            .Where(x => !string.IsNullOrEmpty(x)).ToDictionary(x => int.Parse(x.Split(' ')[0]), x => uint.Parse(x.Split(' ')[1]));

}