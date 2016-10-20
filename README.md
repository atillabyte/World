# World [![Build status](https://ci.appveyor.com/api/projects/status/x5ip07f7fbv01t7d?svg=true)](https://ci.appveyor.com/project/atillabyte/world)

######  :grey_exclamation: This library is currently _read-only_; world properties cannot be modified.
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
> Synchronising worlds from JSON.
```csharp
World.CLI.exe sync -u:email -a:password -i:world.json -t:PWTargetEI
```

> Generating world minimaps from JSON.
```csharp
World.CLI.exe create -t:minimap -i:PWTargetEI -o:world.png  
```

> Exporting world to JSON.
```csharp
World.CLI.exe create -t:exportjson -i:PWTargetEI -o:world.json  
```

# World.Web
[World.Web](https://atillabyte.github.io/World) `This utility is currently in beta - see gh-pages branch.`
