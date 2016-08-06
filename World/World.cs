using System;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayerIOClient;

public class World : Dynamic
{
    public List<Block> Blocks;
    public class Block : Dynamic
    {
        public List<Position> Positions { get; set; } = new List<Position>();
        public struct Position
        {
            public uint X { get; set; }
            public uint Y { get; set; }
        }
    }

    public enum WorldType { BigDB, JSON };
    private WorldType _input;
    private DatabaseObject _world;
    public World(WorldType input, Client client, params string[] arguments)
    {
        this._input = input;

        switch (input) {
            case WorldType.BigDB: {
                    var world = client.BigDB.Load("worlds", arguments[0]);
                    this._world = world;
                    this.Blocks = world.GetArray("worlddata").FromWorldData();

                    foreach (var property in world)
                        if (property.Key != "worlddata")
                            this[property.Key] = property.Value;

                }
                break;
            case WorldType.JSON: {
                    var world = File.ReadAllText(arguments[0]);
                    this.Blocks = world.FromJsonArray();

                    foreach (var property in JObject.Parse(world))
                        if (property.Key != "worlddata")
                            this[property.Key] = property.Value;
                }
                break;
        }
    }

    public enum NotationType { JSON };
    public string Serialize(NotationType notation, params string[] arguments)
    {
        switch (notation) {
            case NotationType.JSON:
                return JsonConvert.SerializeObject(_world.ExtractDatabaseObject());
        }

        return null;
    }
}

public static class Helpers
{
    public static List<World.Block> FromWorldData(this DatabaseArray input)
    {
        var blocks = new List<World.Block>();

        if (input == null || !input.Any())
            return blocks;

        for (int i = 0; i < input.Count; i++) {
            if (input.Contains(i) && input.GetObject(i).Count != 0) {
                var obj = input.GetObject(i);
                dynamic temp = new World.Block();

                foreach (var kvp in obj) temp[kvp.Key] = kvp.Value;

                byte[] x = obj.TryGetBytes("x", new byte[0]), y = obj.TryGetBytes("y", new byte[0]);
                byte[] x1 = obj.TryGetBytes("x1", new byte[0]), y1 = obj.TryGetBytes("y1", new byte[0]);

                for (int j = 0; j < x1.Length; j++)
                    temp.Positions.Add(new World.Block.Position() { X = (uint)x1[j], Y = (uint)y1[j] });
                for (int k = 0; k < x.Length; k += 2)
                    temp.Positions.Add(new World.Block.Position() { X = (uint)(((int)x[k] << 8) + (int)x[k + 1]), Y = (uint)(((int)y[k] << 8) + (int)y[k + 1]) });

                blocks.Add(temp);
            }
        }

        return blocks;
    }
    public static List<World.Block> FromJsonArray(this string input)
    {
        var world = JObject.Parse(input);
        var array = world["worlddata"].Values().Select(x => (IEnumerable<JToken>)x);

        var temp = new DatabaseArray();

        foreach (var block in array) {
            var dbo = new DatabaseObject();

            temp.Add(block.Select(b => (JProperty)b).Select(p => (p.Value.Type == JTokenType.Integer) ? dbo.Set(p.Name, (int)(uint)p.Value) :
                                         (p.Value.Type == JTokenType.Boolean) ? dbo.Set(p.Name, (bool)p.Value) :
                                         (p.Value.Type == JTokenType.Float) ? dbo.Set(p.Name, (double)p.Value) : dbo.Set(p.Name, (string)p.Value)).Last());
        }

        return FromWorldData(temp);
    }

    private static byte[] TryGetBytes(this DatabaseObject input, string propertyExpression, byte[] defaultValue)
    {
        object obj = null;

        if (input.TryGetValue(propertyExpression, out obj))
            return (obj is string) ? Convert.FromBase64String(obj as string) : (obj is byte[]) ? obj as byte[] : defaultValue;

        return defaultValue;
    }
    public static object ExtractDatabaseObject(this object input)
    {
        var _dict = new Dictionary<object, object>();

        switch (input.GetType().Name) {
                case "DatabaseObject":  case "identifier916":
                foreach (var o in ((DatabaseObject)input))
                    _dict.Add(o.Key, ExtractDatabaseObject(o.Value));
                break;
            case "DatabaseArray":  case "identifier917":
                foreach (var o in ((DatabaseArray)input).IndexesAndValues)
                    _dict.Add(o.Key, ExtractDatabaseObject(o.Value));
                break;
            default: return input;
        }

        return _dict;
    }
}

public abstract class Dynamic : DynamicObject
{
    private Dictionary<string, object> dictionary = new Dictionary<string, object>();
    public int Count { get { return dictionary.Count; } }

    public object this[string key] { get { return (dictionary.ContainsKey(key)) ? dictionary[key] : null; } set { dictionary[key] = value; } }
    public List<KeyValuePair<string, object>> Values => dictionary.ToList();
    public override bool TryGetMember(GetMemberBinder binder, out object result) => dictionary.TryGetValue(binder.Name.ToLower(), out result);
    public override bool TrySetMember(SetMemberBinder binder, object value) { dictionary[binder.Name.ToLower()] = value; return true; }
}