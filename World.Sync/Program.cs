using PlayerIOClient;
using System;

internal class Program
{
    private static void Main(string[] args)
    {
        var client = PlayerIO.QuickConnect.SimpleConnect("everybody-edits-su9rn58o40itdbnw69plyw", "email", "password", null);
        var con = client.Multiplayer.CreateJoinRoom("WorldId", "Everybodyedits" + client.BigDB.Load("config", "config")["version"], true, null, null);
        con.Send("init");

        con.OnMessage += delegate (object s, Message e)
        {
            if (e.Type == "init")
            {
                var world = new World(World.Input.JSON, new World.Options()
                {
                    Client = client,
                    Connection = con,
                    Target = "WorldId",
                    Source = @"X:\world.json"
                }).Upload();
            }
        };

        Console.ReadLine();
    }
}