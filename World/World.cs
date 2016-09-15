using System;
using System.Collections.Generic;
using PlayerIOClient;
using Newtonsoft.Json.Linq;
using System.Drawing;

public partial class World : PropertyEnumerable
{
    public List<Block> Blocks = new List<Block>();

    public enum InputType { JSON, BigDB }
    public World(InputType type, string input, Client client = null)
    {
        dynamic world = null;
        switch (type) {
            case InputType.BigDB:
                world = client.BigDB.Load("worlds", input);

                foreach (var property in (DatabaseObject)world)
                    _properties.Add(property.Key, property.Value);

                this.Blocks = Helpers.FromWorldData(world.GetArray("worlddata"));
                break;
            case InputType.JSON:
                world = JObject.Parse(input);

                foreach (var property in world)
                    _properties.Add(property.Name, property.Value);

                this.Blocks = Helpers.FromJsonArray(world);
                break;
        }
    }
}

/// <summary>
/// This class is for intended as ease-of-use for common applications.
/// If a property is not referenced, it can alternatively be accessed through world["propertyName"]
/// </summary>
public partial class World
{
    public string Name => _properties.Get<string>("name") != null ? _properties.Get<string>("name") : "Untitled World";
    public string Owner => _properties.Get<string>("owner");
    public string Crew => _properties.Get<string>("Crew");
    public string Description => _properties.Get<string>("worldDescription");

    public int Type => _properties.Get<int>("type");
    public int Width => _properties.ContainsKey("width", false) ? _properties.Get<int>("height") : 200;
    public int Height => _properties.ContainsKey("width", false) ? _properties.Get<int>("height") : 200;

    public int Plays => _properties.Get<int>("plays");
    public int Woots => _properties.Get<int>("woots");
    public int TotalWoots => _properties.Get<int>("totalwoots");
    public int Likes => _properties.Get<int>("Likes");
    public int Favorites => _properties.Get<int>("Favorites");

    public bool Visible => _properties.Get<bool>("visible");
    public bool HideLobby => _properties.Get<bool>("HideLobby");

    [Obsolete("Potions are no longer available.")]
    public bool AllowPotions => _properties.Get<bool>("allowpotions");

    public Color BackgroundColor
    {
        get {
            if (!_properties.ContainsKey("backgroundColor", false))
                return Color.FromArgb(-16777216);

            var value = _properties.Get<uint>("backgroundColor");
            var color = Color.FromArgb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)(value >> 0));

            return color;
        }
    }

    public class Block : PropertyEnumerable
    {
        public int Type => Convert.ToInt32(_properties.Get<object>("type"));
        public int Layer => Convert.ToInt32(_properties.Get<object>("layer"));

        public int Rotation => _properties.Get<int>("rotation");
        public int Goal => _properties.Get<int>("goal");

        public object Id => _properties.Get<object>("id");
        public object Target => _properties.Get<object>("target");

        public string Text => _properties.Get<string>("text");
        public int SignType => _properties.Get<int>("signtype");

        public List<Location> Locations = new List<Location>();
        public class Location
        {
            public int X { get; set; }
            public int Y { get; set; }
        }
    }
}

public static class Helpers
{
    private static Dictionary<object, object> ToDictionary(this DatabaseObject dbo, object input)
    {
        var dict = new Dictionary<object, object>();
        var spec = new List<string>() { "DatabaseObject", "identifier916", "DatabaseArray", "identifier917" };

        switch (input.GetType().Name) {
            case "DatabaseObject":
            case "identifier916":
                foreach (var o in ((DatabaseObject)input))
                    dict.Add(o.Key, spec.Contains(o.Value.GetType().Name) ? ToDictionary(null, o.Value) : o.Value);
                break;
            case "DatabaseArray":
            case "identifier917":
                foreach (var o in ((DatabaseArray)input).IndexesAndValues)
                    dict.Add(o.Key, spec.Contains(o.Value.GetType().Name) ? ToDictionary(null, o.Value) : o.Value);
                break;
        }

        return dict;
    }
    public static Dictionary<object, object> ToDictionary(this DatabaseObject dbo) => ToDictionary(dbo, dbo);

    public static T Get<T>(this Dictionary<string, object> dictionary, string key)
    {
        if (dictionary.ContainsKey(key.ToLower()))
            foreach (var kvp in dictionary)
                if (kvp.Key.ToLower() == key.ToLower())
                    if (kvp.Value.GetType() == typeof(JValue))
                        return ((JValue)kvp.Value).ToObject<T>();
                    else
                        return (T)kvp.Value;

        return default(T);
    }

    public static bool ContainsKey(this Dictionary<string, object> dictionary, string key, bool sensitive)
    {
        if (!sensitive) {
            foreach (var k in dictionary.Keys)
                if (k.ToLower() == key.ToLower())
                    return true;
        } else
            return dictionary.ContainsKey(key);

        return false;
    }

    private static byte[] TryGetBytes(this DatabaseObject input, string key, byte[] defaultValue)
    {
        object obj = null;

        if (input.TryGetValue(key, out obj))
            return (obj is string) ? Convert.FromBase64String(obj as string) : (obj is byte[]) ? obj as byte[] : defaultValue;

        return defaultValue;
    }

    public static List<World.Block> FromWorldData(this DatabaseArray input)
    {
        var blocks = new List<World.Block>();

        if (input == null || input.Count == 0)
            return blocks;

        for (int i = 0; i < input.Count; i++) {
            if (input.Contains(i) && input.GetObject(i).Count != 0) {
                var obj = input.GetObject(i);
                dynamic temp = new World.Block();

                foreach (var kvp in obj)
                    temp[kvp.Key] = kvp.Value;

                byte[] x = obj.TryGetBytes("x", new byte[0]), y = obj.TryGetBytes("y", new byte[0]);
                byte[] x1 = obj.TryGetBytes("x1", new byte[0]), y1 = obj.TryGetBytes("y1", new byte[0]);

                for (int j = 0; j < x1.Length; j++)
                    temp.Locations.Add(new World.Block.Location() { X = x1[j], Y = y1[j] });
                for (int k = 0; k < x.Length; k += 2)
                    temp.Locations.Add(new World.Block.Location() { X = (x[k] << 8) + x[k + 1], Y = (y[k] << 8) + y[k + 1] });

                blocks.Add(temp);
            }
        }

        return blocks;
    }
    public static List<World.Block> FromJsonArray(this JObject world)
    {
        var array = world["worlddata"].Values().AsJEnumerable();
        var temp = new DatabaseArray();

        foreach (var block in array) {
            var dbo = new DatabaseObject();

            foreach (var token in block) {
                var property = (JProperty)token;
                var value = property.Value;

                switch (value.Type) {
                    case JTokenType.Integer:
                        dbo.Set(property.Name, (uint)value);
                        break;
                    case JTokenType.Boolean:
                        dbo.Set(property.Name, (bool)value);
                        break;
                    case JTokenType.Float:
                        dbo.Set(property.Name, (double)value);
                        break;
                    default:
                        dbo.Set(property.Name, (string)value);
                        break;
                }
            }

            temp.Add(dbo);
        }

        return FromWorldData(temp);
    }
}

public class PropertyEnumerable
{
    internal Dictionary<string, object> _properties = new Dictionary<string, object>();

    public Dictionary<string, object> Properties => _properties;
    public object this[string key]
    {
        get { return _properties.ContainsKey(key) ? _properties[key] : null; }
        set {
            if (_properties.ContainsKey(key))
                _properties.Add(key, value);
            else
                _properties[key] = value;
        }
    }
}