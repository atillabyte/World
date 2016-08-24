using PlayerIOClient;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// This project is for testing purposes, it should not be used as a demonstration.
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        SerializeFromJsonInput();
    }

    static void SerializeFromJsonInput()
    {
        var client = PlayerIO.QuickConnect.SimpleConnect("everybody-edits-su9rn58o40itdbnw69plyw", "guest", "guest", null);
        var world = new World(World.WorldType.JSON, client, @"X:\Projects\PW1WVcN1OQb0I_188.json");

        var output = world.Serialize(World.NotationType.JSON, "indented");
        File.WriteAllText(@"X:\Projects\out.json", output);

        foreach (KeyValuePair<string, object> property in world)
            if (property.Key != "worlddata")
                Console.WriteLine(property);
    }
}