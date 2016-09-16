# World
Everybody Edits World Library

This library allows seamless interaction with Everybody Edits Worlds.

# Features
- A very simple class which allows for easy interaction with world properties.
- A future-proof design, block properties automatically in proper order in Message packet.
- A modern and rebust format for serialization and deserialization - JSON.
- A command-line utility serving as functional examples (i.e. creating minimaps and synchronizing worlds to/from JSON).


# Examples
> Accessing world properties

```csharp
var world = new World(InputType.BigDB, "PW01");

var plays = world.Plays;
var name = world.Name;

var coins = world.Blocks.Where(x => x.Type == 100 || x.Type == 101).Select(x => x.Locations.Count()).Sum();
```

> Serialization and deserialization

```csharp
var serialized =  new World(InputType.BigDB, "PW01").Serialize();
var deserialized = new World(InputType.JSON, serialized, null);
```

# World.CLI
> Documentation is in progress, you may look at [World.CLI](https://github.com/atillabyte/World/blob/master/World.CLI/Program.cs) in the meantime.
