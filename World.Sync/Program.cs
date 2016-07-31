using System;
using System.Threading;
using PlayerIOClient;

internal class Program
{
    static void Main(string[] args)
    {
        var client = PlayerIO.QuickConnect.SimpleConnect("everybody-edits-su9rn58o40itdbnw69plyw", "email", "password", null);
        var con = client.Multiplayer.CreateJoinRoom("WorldId", "Everybodyedits" + client.BigDB.Load("config", "config")["version"], true, null, null);

        con.OnMessage += delegate (object s, Message e)
        {
            if (e.Type == "init") {
                Thread.Sleep(1000);
                con.Send("save");
            }

            if (e.Type == "saved") {
                var world = new World(World.Input.JSON, new World.Options() {
                    Client = client,
                    Connection = con,
                    Target = "WorldId",
                    Source = @"world.json"
                });

                var status = world.Upload();

                Console.WriteLine("Status: " + status.ToString());
                switch (status) {
                    case World.Status.Incompleted:
                        Main(new string[] { });
                        break;
                    case World.Status.Completed:
                        con.Disconnect();
                        Console.WriteLine("Status: " + status.ToString());
                        break;
                }
            }
        };

        con.Send("init");
        Console.ReadLine();
    }
}