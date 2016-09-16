using System;
using System.IO;
using CLAP;
using Newtonsoft.Json;
using static World;

class Program
{
    static int Main(string[] args) => Parser.RunConsole<Interface>(args);
}

class Interface
{
    public enum CreateType { Minimap, ExportJSON }

    [Verb(Aliases = "c")]
    public static void Create([Aliases("t")]string type, [Aliases("i")]string input, [Aliases("o")]string output)
    {
        switch ((CreateType)Enum.Parse(typeof(CreateType), type, true)) {
            case CreateType.Minimap:
                var minimap = File.Exists(input) ? 
                    Minimap.Create(new World(InputType.JSON, File.ReadAllText(input), null)) :
                    Minimap.Create(new World(InputType.BigDB, input, null));

                minimap.Save(Path.GetFullPath(output));
                break;
            case CreateType.ExportJSON:
                File.WriteAllText(Path.GetFullPath(output), new World(InputType.BigDB, input, null).Serialize(OutputType.JSON));
                break;
        }

        Environment.Exit(0);
    }

    [Verb(Aliases = "s")]
    public static void Sync([Aliases("u")]string user, [Aliases("a")]string auth, [Aliases("i")]string input, [Aliases("t")]string target)
    {
        var client = PlayerIOClient.Helpers.Authentication.LogOn("everybody-edits-su9rn58o40itdbnw69plyw", user, auth);
        var world = File.Exists(input) ? new World(InputType.JSON, File.ReadAllText(input), client) : new World(InputType.BigDB, input, client);
        var sync = new SyncWorld(world, client, target, null);

        sync.OnTimeout += () => { Environment.Exit(-1); };
        sync.OnCompleted += () => { Environment.Exit(0); };

        System.Threading.Thread.Sleep(-1);
    }
}
