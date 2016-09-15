using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlayerIOClient;

public class SyncWorld
{
    public event OnCompleteHandler OnCompleted;
    public delegate void OnCompleteHandler();


    public event OnTimeoutHandler OnTimeout;
    public delegate void OnTimeoutHandler();

    public SyncWorld(World world, Client client, string targetId, Connection connection, int retries = 0)
    {
        if (connection == null || !connection.Connected)
            connection = client.Multiplayer.CreateJoinRoom(targetId, "", true, null, null);

        connection.OnMessage += (s, e) => {
            switch (e.Type) {
                case "init":
                    Task.Delay(1000).Wait();

                    connection.Send("save");
                    break;
                case "saved":
                    var status = Upload(world, client, connection, targetId);

                    switch (status) {
                        case Status.Incomplete:
                            retries++;
                            if (retries < 16)
                                connection.Send("save");
                            else {
                                connection.Disconnect();
                                OnTimeout.Invoke();
                            }
                            break;
                        case Status.Completed:
                            OnCompleted.Invoke();
                            break;
                    }
                    break;
            }
        };

        connection.OnDisconnect += (s, e) => {
            var sync = new SyncWorld(world, client, targetId, connection, retries);
        };

        connection.Send("init");
    }

    public enum Status { Incomplete, Completed }
    public Status Upload(World world, Client client, Connection connection, string targetId)
    {
        var target = new World(World.InputType.BigDB, targetId, client);

        var filter = new List<string>() { "type", "layer", "x", "y", "x1", "y1" };
        var packets = new List<Message>();

        foreach (var block in world.Blocks)
            foreach (var location in block.Locations)
                if (!target.Blocks.Any(x => x.Type == block.Type && x.Layer == block.Layer && (x.Locations.Any(p => p.X == location.X && p.Y == location.Y))))
                    packets.Add(new Func<Message>(() => {
                        var packet = Message.Create("b", block.Layer, location.X, location.Y, block.Type);
                        packet.Add(block.Properties.Where(x => !filter.Contains(x.Key)).Select(x => block[x.Key]).ToArray());

                        return packet;
                    }).Invoke());

        foreach (var block in packets)
            if (connection.Connected)
                Task.Run(async () => { connection.Send(block); await Task.Delay(10); }).Wait();
            else
                return Status.Incomplete;

        return Status.Completed;
    }
}