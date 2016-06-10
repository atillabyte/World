using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Collections.Generic;

using PlayerIOClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class World
{
    private List<Block> Queue { get; set; }
    private Options Config { get; set; }
    private DatabaseObject Source { get; set; }

    public enum Input { BigDB, JSON }
    public class Options
    {
        public string Source { get; set; }
        public string Target { get; set; }

        public Client Client { get; set; }
        public Connection Connection { get; set; }
    }

    public World(Input input, Options config)
    {
        this.Config = config;
        switch (input)
        {
            case Input.BigDB:
                this.Source = config.Client.BigDB.Load("worlds", config.Source);
                this.Queue = Source.GetArray("worlddata").FromWorldData();
                break;
            case Input.JSON:
                this.Queue = config.Source.FromJsonArray();
                break;
        }
    }

    public enum Status { Incompleted, Completed }
    public Status Upload()
    {
        var target = this.Config.Client.BigDB.Load("worlds", this.Config.Target).GetArray("worlddata").FromWorldData();

        var filter = new List<string>() { "type", "layer", "x", "y", "x1", "y1" };
        var normal = this.Queue.Where(p => !p.Source.Properties.Except(filter).Any()).ToList();
        var special = this.Queue.Where(p => p.Source.Properties.Except(filter).Any()).ToList();

        var packets = new List<Message>();

        foreach (var block in normal)
            foreach (var position in block.Positions)
                packets.Add(Message.Create("b", block.Layer, position.X, position.Y, block.Type));

        foreach (var block in special)
            foreach (var position in block.Positions)
                packets.Add(new Func<Message>(() =>
                {
                    block.HandleCustomRules();

                    var packet = Message.Create("b", block.Layer, position.X, position.Y, block.Type);
                        packet.Add(block.Source.Properties.Except(filter).Select(s => block.Source[s]).ToArray());

                    return packet;
                }).Invoke());

        foreach (var block in packets)
            if (this.Config.Connection.Connected) Task.Run(async () => { Console.WriteLine(block); this.Config.Connection.Send(block); await Task.Delay(16); }).Wait();
            else return Status.Incompleted;

        return Status.Completed;
    }

    /// <summary>Saves the world to the specified directory.</summary>
    public void Save(string path)
    {
        if (Source == null)
            throw new Exception("The specified World Source is null.");

        this.Source.Save(path);
    }
    
    /// <summary>Refreshes the current Source world.</summary>
    public void Refresh()
    {
        if (string.IsNullOrEmpty(this.Config.Source))
            throw new Exception($"This method can only be used in { string.Join(", ", new List<Input>() { Input.BigDB }) } contexts.");

        this.Source = this.Config.Client.BigDB.Load("worlds", this.Config.Source);
    }

    public class Block
    {
        public DatabaseObject Source { get; set; }
        public List<Position> Positions { get; set; } = new List<Position>();
        public uint Type => Convert.ToUInt32(Source.GetValue("type"));
        public uint Layer => Source.Contains("layer") ? Convert.ToUInt32(Source.GetValue("layer")) : 0;

        public class Position
        {
            public uint X { get; set; }
            public uint Y { get; set; }
        }
    }
}
static internal class Helpers
{
    public static List<World.Block> FromWorldData(this DatabaseArray source)
    {
        var blocks = new List<World.Block>();

        for (int i = 0; i < source.Count; i++)
        {
            if (source.Contains(i) && source.GetObject(i).Count != 0)
            {
                var obj = source.GetObject(i);
                var temp = new World.Block() { Source = obj };

                byte[] x = obj.GetBytes("x", new byte[0]),
                       y = obj.GetBytes("y", new byte[0]);
                byte[] x1 = obj.GetBytes("x1", new byte[0]),
                       y1 = obj.GetBytes("y1", new byte[0]);

                for (int j = 0; j < x1.Length; j++)
                {
                    byte nx = x1[j],
                         ny = y1[j];

                    temp.Positions.Add(new World.Block.Position() { X = (uint)nx, Y = (uint)ny });
                }
                for (int k = 0; k < x.Length; k += 2)
                {
                    uint nx2 = (uint)(((int)x[k] << 8) + (int)x[k + 1]),
                         ny2 = (uint)(((int)y[k] << 8) + (int)y[k + 1]);

                    temp.Positions.Add(new World.Block.Position() { X = (uint)nx2, Y = (uint)ny2 });
                }

                blocks.Add(temp);
            }
        }

        return blocks;
    }
    public static List<World.Block> FromJsonArray(this string source)
    {
        var world = JObject.Parse(File.ReadAllText(source));
        var array = world["worlddata"].Values().Select(x => (IEnumerable<JToken>)x);
        var queue = new List<World.Block>();

        foreach (var block in array)
        {
            var properties = block.Select(b => (JProperty)b);
            var dbo = new DatabaseObject();
            var temp = new World.Block();

            dbo = properties.Select(p =>
                 (p.Value.Type == JTokenType.Integer) ? dbo.Set(p.Name, p.Value.ToObject<uint>()) :
                 (p.Value.Type == JTokenType.Boolean) ? dbo.Set(p.Name, p.Value.ToObject<bool>()) : dbo.Set(p.Name, p.Value.ToObject<string>())).Last();

            byte[] x = (!string.IsNullOrEmpty(dbo.GetString("x", ""))) ? Convert.FromBase64String(dbo.GetString("x")).HandleCompression() : new byte[0],
                   y = (!string.IsNullOrEmpty(dbo.GetString("y", ""))) ? Convert.FromBase64String(dbo.GetString("y")).HandleCompression() : new byte[0],
                   x1 = (!string.IsNullOrEmpty(dbo.GetString("x1", ""))) ? Convert.FromBase64String(dbo.GetString("x1")).HandleCompression() : new byte[0],
                   y1 = (!string.IsNullOrEmpty(dbo.GetString("y1", ""))) ? Convert.FromBase64String(dbo.GetString("y1")).HandleCompression() : new byte[0];

            for (int j = 0; j < x1.Length; j++)
            {
                byte nx = x1[j],
                     ny = y1[j];

                temp.Positions.Add(new World.Block.Position() { X = (uint)nx, Y = (uint)ny });
            }

            for (int k = 0; k < x.Length; k += 2)
            {
                uint nx2 = (uint)(((int)x[k] << 8) + (int)x[k + 1]),
                     ny2 = (uint)(((int)y[k] << 8) + (int)y[k + 1]);

                temp.Positions.Add(new World.Block.Position() { X = (uint)nx2, Y = (uint)ny2 });
            }

            temp.Source = dbo;
            queue.Add(temp);
        }
        return queue;
    }

    /// This shouldn't really exist; wish not want not, lest they fix the server code. ~atillabyte
    public static void HandleCustomRules(this World.Block block)
    {
        switch (block.Type)
        {
            case 385:
                if (!block.Source.Contains("signtype"))
                    block.Source.Set("signtype", 0);
                break;
        }
    }
    public static byte[] HandleCompression(this byte[] property)
    {
        if (Compression.IsGZipHeader(property))
            property = Compression.Decompress(property);

        return property;
    }

    public static object Extract(this object input)
    {
        var _dict = new Dictionary<object, object>();

        switch (input.GetType().Name)
        {
            case "DatabaseObject":
            case "identifier916":
                foreach (var o in ((DatabaseObject)input))
                    _dict.Add(o.Key, Extract(o.Value));
                break;
            case "DatabaseArray":
            case "identifier917":
                foreach (var o in ((DatabaseArray)input).IndexesAndValues)
                    _dict.Add(o.Key, Extract(o.Value));
                break;
            default:
                return input;
        }

        return _dict;
    }
    public static void Save(this DatabaseObject input, string path)
    {
        if (input == null)
            throw new ArgumentNullException("input", "The specified DatabaseObject is null.");

        File.WriteAllText(JsonConvert.SerializeObject(input.Extract()), path);
    }

    class Compression
    {
        public static bool IsGZipHeader(byte[] arr)
        {
            return arr.Length >= 2 &&
                arr[0] == 31 &&
                arr[1] == 139;
        }

        public static byte[] Compress(byte[] raw)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(memory,
                CompressionMode.Compress, true))
                {
                    gzip.Write(raw, 0, raw.Length);
                }
                return memory.ToArray();
            }
        }
        public static byte[] Decompress(byte[] gzip)
        {
            using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
            {
                const int size = 2048;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return memory.ToArray();
                }
            }
        }
    }
}