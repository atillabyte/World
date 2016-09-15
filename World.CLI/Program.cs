using System;
using CLAP;
using static World;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        Parser.Run<Interface>(Console.ReadLine().Split(' '));

        System.Threading.Thread.Sleep(-1);
    }
}

class Interface
{
    public enum CreateType { Minimap }

    [Verb(Aliases = "c")]
    public static void Create([Aliases("t")]string type, [Aliases("i")]string input, [Aliases("o")]string output)
    {
        switch ((CreateType)Enum.Parse(typeof(CreateType), type, true)) {
            case CreateType.Minimap:
                if (File.Exists(input))
                    Minimap.Create(new World(InputType.JSON, File.ReadAllText(input), null)).Save(Path.GetFullPath(output));
                else
                    Minimap.Create(new World(InputType.BigDB, input, null));
                break;
        }
    }

    [Verb(Aliases = "s")]
    public static void Sync([Aliases("u")]string user, [Aliases("a")]string auth, [Aliases("i")]string input, [Aliases("t")]string target)
    {
        var client = PlayerIOClient.Helpers.Authentication.LogOn("everybody-edits-su9rn58o40itdbnw69plyw", user, auth);
        if (File.Exists(input)) {
            var world = new World(InputType.JSON, input, client);
            var sync = new SyncWorld(world, client, target, null);
        } else {
            var world = new World(InputType.BigDB, input, client);
            var sync = new SyncWorld(world, client, target, null);
        }

        Console.WriteLine("Completed!");
        Console.ReadLine();
    }
}
