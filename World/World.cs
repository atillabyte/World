using System;
using System.IO;
using System.Linq;
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
        var packets = new List<Message>();

        foreach (var block in this.Queue)
            foreach (var position in block.Positions)
                if (!target.Any(x => x.Type == block.Type && x.Layer == block.Layer && x.Positions.Any(p => p.X == position.X && p.Y == position.Y)))
                    packets.Add(new Func<Message>(() => {
                        block.HandleCustomRules();
                        var packet = Message.Create("b", block.Layer, position.X, position.Y, block.Type);
                        packet.Add(block.Source.Properties.Except(filter).Select(s => block.Source[s]).ToArray());

                        return packet;
                    }).Invoke());

        foreach (var block in packets)
            if (this.Config.Connection.Connected)
                Task.Run(async() => { Console.WriteLine(block); this.Config.Connection.Send(block); await Task.Delay(8); }).Wait();
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
        public uint Type => Source.Contains("type") ? Convert.ToUInt32(Source.GetValue("type")) : 0;
        public uint Layer => Source.Contains("layer") ? Convert.ToUInt32(Source.GetValue("layer")) : 0;

        public class Position
        {
            public uint X { get; set; }
            public uint Y { get; set; }
        }
    }
}

static class Helpers
{
    public static List<World.Block> FromWorldData(this DatabaseArray source)
    {
        var blocks = new List<World.Block>();

        if (source == null || !source.Any())
            return blocks;

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
                    temp.Positions.Add(new World.Block.Position() { X = (uint)x1[j], Y = (uint)y1[j] });
                for (int k = 0; k < x.Length; k += 2)
                    temp.Positions.Add(new World.Block.Position() { X = (uint)(((int)x[k] << 8) + (int)x[k + 1]), Y = (uint)(((int)y[k] << 8) + (int)y[k + 1]) });

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
                 (p.Value.Type == JTokenType.Integer) ? dbo.Set(p.Name, (int)(uint)p.Value) :
                 (p.Value.Type == JTokenType.Boolean) ? dbo.Set(p.Name, (bool)p.Value) :
                 (p.Value.Type == JTokenType.Float) ? dbo.Set(p.Name, (double)p.Value) : dbo.Set(p.Name, (string)p.Value)).Last();

            byte[] x = (!string.IsNullOrEmpty(dbo.GetString("x", ""))) ? Convert.FromBase64String(dbo.GetString("x")) : new byte[0],
                   y = (!string.IsNullOrEmpty(dbo.GetString("y", ""))) ? Convert.FromBase64String(dbo.GetString("y")) : new byte[0],
                   x1 = (!string.IsNullOrEmpty(dbo.GetString("x1", ""))) ? Convert.FromBase64String(dbo.GetString("x1")) : new byte[0],
                   y1 = (!string.IsNullOrEmpty(dbo.GetString("y1", ""))) ? Convert.FromBase64String(dbo.GetString("y1")) : new byte[0];

            for (int j = 0; j < x1.Length; j++)
                temp.Positions.Add(new World.Block.Position() { X = x1[j], Y = y1[j] });
            for(int k = 0; k < x.Length; k += 2)
                temp.Positions.Add(new World.Block.Position() { X = (uint)((x[k] << 8) + x[k + 1]), Y = (uint)((y[k] << 8) + y[k + 1]) });

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

        File.WriteAllText(path, JsonConvert.SerializeObject(input.Extract()));
    }
}