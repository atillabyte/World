using System;
using System.Threading;
using PlayerIOClient;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

internal class Program
{
    static void Main(string[] args)
    {
        string targetId = "PWInputAn_IdEI";
        string inputFile = "PWInputAn_IdEI.json";

        var client = PlayerIO.QuickConnect.SimpleConnect("everybody-edits-su9rn58o40itdbnw69plyw", "email", "password", null);
        var con = client.Multiplayer.CreateJoinRoom(targetId, "Everybodyedits" + client.BigDB.Load("config", "config")["version"], true, null, null);

        con.OnMessage += delegate (object s, Message e)
        {
            if (e.Type == "init") {
                Thread.Sleep(1000);
                con.Send("save");
            }

            if (e.Type == "saved") {
                var world = new World(World.WorldType.JSON, client, inputFile);
                var status = Upload(world, client, con, targetId);

                switch (status) {
                    case Status.Incompleted:
                        Main(new string[] { });
                        break;
                    case Status.Completed:
                        con.Disconnect();
                        Console.WriteLine("Status: " + status.ToString());
                        break;
                }
            }
        };

        con.Send("init");
        Console.ReadLine();
    }

    public enum Status { Incompleted, Completed }
    public static Status Upload(World world, Client client, Connection connection, string targetId)
    {
        var target = client.BigDB.Load("worlds", targetId).GetArray("worlddata").FromWorldData().Cast<dynamic>();

        var filter = new List<string>() { "type", "layer", "x", "y", "x1", "y1" };
        var packets = new List<Message>();

        foreach (dynamic block in world.Blocks as List<World.Block>)
            foreach (var position in block.Positions)
                if (!target.Any(x => x.Type == block.Type && x.Layer == block.Layer && ((IEnumerable<dynamic>)x.Positions).Any(p => p.X == position.X && p.Y == position.Y)))
                    packets.Add(new Func<Message>(() => {
                        var packet = Message.Create("b", block.Layer, position.X, position.Y, block.Type);
                        packet.Add(((List<KeyValuePair<string, object>>)block.Values).Where(x => !filter.Contains(x.Key)).Select(x => block[x.Key]).ToArray());

                        return packet;
                    }).Invoke());

        foreach (var block in packets)
            if (connection.Connected)
                Task.Run(async () => { Console.WriteLine(block); connection.Send(block); await Task.Delay(8); }).Wait();
            else
                return Status.Incompleted;

        return Status.Completed;
    }
}