using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using PlayerIOClient;
using ConsoleTables.Core;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Reports;
using static World;

class Program
{
    static void Main(string[] args)
    {
        var benchmarks = new List<Summary>() {
            BenchmarkRunner.Run<MinimapBenchmark>()
        };
        Console.Clear();

        Console.WriteLine("Benchmarks:");
        var table = new ConsoleTable("Method", "Median Time (nanoseconds)");

        foreach (var summary in benchmarks)
            foreach (var report in summary.Reports)
                table.AddRow(report.Benchmark.Target.MethodTitle, report.ResultStatistics.Median / 1000000d + " ms");
       
        Console.WriteLine(table.ToMarkDownString());

        Console.WriteLine("World Properties:");
        EvaluateProperties();

        Thread.Sleep(-1);
    }

    static void EvaluateProperties()
    {
        var world = new World(InputType.BigDB, "PW01");
        var jsonworld = new World(InputType.JSON, world.Serialize());

        var table = new ConsoleTable("Key", "Value", "Type");
        foreach (var property in world.Properties)
            Debug.Assert(jsonworld.Properties[property.Key] == property.Value);

        foreach (var property in world.Properties.Where(x => x.Key != "worlddata"))
            table.AddRow(property.Key, property.Value, property.Value.GetType().Name);

        Console.WriteLine(table.ToMarkDownString());
    }
}

public class MinimapBenchmark
{
    public Client client;
    public World world;

    public MinimapBenchmark()
    {
        client = PlayerIO.Connect("everybody-edits-su9rn58o40itdbnw69plyw", "public", "user", "", "");
        world = new World(World.InputType.BigDB, "PW01", client);
    }

    [Benchmark]
    public void CreateMinimap()
    {
        var minimap = Minimap.Create(world);
    }
}