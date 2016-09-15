using System;
using System.IO;
using System.Threading;
using static World;
using CLAP;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0) {
            Console.Write("No arguments specified.");
            return;
        }
            
        Parser.Run<Interface>(args);
        Thread.Sleep(-1);
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
                if (File.Exists(input)) {
                    Minimap.Create(new World(InputType.JSON, File.ReadAllText(input), null)).Save(Path.GetFullPath(output));
                    Environment.Exit(0);
                } else {
                    Minimap.Create(new World(InputType.BigDB, input, null)).Save(output);
                    Environment.Exit(0);
                }
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

            sync.OnTimeout += () => { Environment.Exit(-1); };
            sync.OnCompleted += () => { Environment.Exit(0); };
        } else {
            var world = new World(InputType.BigDB, input, client);
            var sync = new SyncWorld(world, client, target, null);

            sync.OnTimeout += () => { Environment.Exit(-1); };
            sync.OnCompleted += () => { Environment.Exit(0); };
        }
    }
}
