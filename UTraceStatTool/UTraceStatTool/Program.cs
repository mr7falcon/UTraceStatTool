using System.Globalization;
using CsvHelper;
using MessagePack;
using UTraceStatTool;
using Timer = UTraceStatTool.Timer;

static Timer[] LoadTimers(string path)
{
    Console.WriteLine("Loading timers");

    using var reader = new StreamReader(path);
    using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
        
    var timersList = csvReader.GetRecords<Timer>().ToList();
    var timers = new Timer[timersList.Max(t => t.Id) + 1];
    timersList.ForEach(t => timers[t.Id] = t);
    
    return timers;
}

static FramesTree LoadFrames(string timersPath, string eventsPath)
{
    var timersMap = LoadTimersMap();
    var timers = LoadTimers(timersPath);
    
    using var reader = new StreamReader(eventsPath);
    using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
    csvReader.Context.TypeConverterCache.AddConverter<float>(new InfinitableFloatConverter());
    var timingEvents = csvReader.GetRecords<TimingEvent>().Where(t => t is { ThreadId: 2 });
    var framesTree = new FramesTree(timingEvents, timers, timersMap);

    SaveTimersMap(timersMap);

    return framesTree;
}

static TimersMap LoadTimersMap()
{
    const string path = "timersmap";

    if (!File.Exists(path))
    {
        Console.WriteLine("No timers map found, creating a new one");
        return new();
    }

    Console.WriteLine("Loading timers map");

    var bytes = File.ReadAllBytes(path);
    return MessagePackSerializer.Deserialize<TimersMap>(bytes);
}

static void SaveTimersMap(in TimersMap timersMap)
{
    const string path = "timersmap";

    Console.WriteLine("Saving timers map");

    var bytes = MessagePackSerializer.Serialize<TimersMap>(timersMap);
    File.WriteAllBytes(path, bytes);
}

static void SaveStats(in StatsContainer stats, string path)
{
    if (!path.EndsWith(".stats"))
    {
        path += ".stats";
    }

    Console.WriteLine("Saving stats to " + path);

    var bytes = MessagePackSerializer.Serialize<StatsContainer>(stats);
    File.WriteAllBytes(path, bytes);
}

static StatsContainer? LoadStats(string path)
{
    if (!path.EndsWith(".stats") || !File.Exists(path))
    {
        return null;
    }

    Console.WriteLine("Loading stats");

    var bytes = File.ReadAllBytes(path);
    return MessagePackSerializer.Deserialize<StatsContainer>(bytes);
}

static FramesTree? LoadFramesTree(Arguments arguments)
{
    var timersFilename = arguments.Argument();
    var timingEventsFilename = arguments.Argument();

    if (timersFilename == null || timingEventsFilename == null)
    {
        Console.Write("Params required: <timers filename> <timing events filename>");
        return null;
    }

    return LoadFrames(timersFilename, timingEventsFilename);
}

static string GenerateStatsFilename()
{
    return $"stats_{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.stats";
}

static StatsContainer? Process(Arguments arguments)
{
    var framesTree = LoadFramesTree(arguments);
    if (framesTree == null)
    {
        return null;
    }

    if (!arguments.Param("stats", out string statsFilename))
    {
        statsFilename = GenerateStatsFilename();
    }

    StatsContainer? stats = null;

    if (!arguments.Flag("r"))
    {
        stats = LoadStats(statsFilename);
    }

    stats ??= new();
    stats.DeriveStats(framesTree);

    SaveStats(stats, statsFilename);

    return stats;
}

static void Compare(Arguments arguments)
{
    StatsContainer? GetStats()
    {
        var statsFilename = arguments.Argument();
        if (statsFilename == null)
        {
            Console.WriteLine("Params required: <stats filename|parse params>");
            return null;
        }

        var stats = LoadStats(statsFilename);
        if (stats == null)
        {
            arguments.RollBack();
            stats = Process(arguments);
        }
    
        return stats;
    }

    var stats1 = GetStats();
    if (stats1 == null)
    {
        return;
    }

    var stats2 = GetStats();
    if (stats2 == null)
    {
        return;
    }

    var timersMap = LoadTimersMap();
    ParseAnalysisFlags(arguments, out var bDump, out var show);

    AnalysisHelpers.CompareStats(stats1, stats2, timersMap, bDump, show);
}

static void Analyze(Arguments arguments)
{
    var framesTree = LoadFramesTree(arguments);
    if (framesTree == null)
    {
        return;
    }

    StatsContainer? stats = null;
    
    if (!arguments.Param("stats", out string statsFilename))
    {
        stats = new();
        stats.DeriveStats(framesTree);

        SaveStats(stats, GenerateStatsFilename());
    }
    else
    {
        stats = LoadStats(statsFilename);
        if (stats == null)
        {
            Console.WriteLine("Failed to load stats");
            return;
        }
    }

    var frames = framesTree.Frames.Select((f, i) => (i, f));
    if (arguments.Param("f", out int index))
    {
        frames = frames.Where(f => f.i == index).Take(1);
    }
    else
    {
        if (arguments.Param("ts", out float start))
        {
            frames = frames.Where(f => f.f.Event.StartTime >= start);
        }

        if (arguments.Param("te", out float end))
        {
            frames = frames.Where(f => f.f.Event.EndTime <= end);
        }
    }

    var timersMap = LoadTimersMap();
    ParseAnalysisFlags(arguments, out var bDump, out var show);

    AnalysisHelpers.AnalyzeFrames(frames, stats, timersMap, bDump, show);
}

static void ParseAnalysisFlags(Arguments arguments, out bool bDump, out int show)
{
    bDump = arguments.Flag("d");
    if (!arguments.Param("s", out show))
    {
        show = -1;
    }
}

static void PrintStat(Arguments arguments)
{
    var statsFilename = arguments.Argument();
    if (statsFilename == null)
    {
        Console.WriteLine("Params required: <stats filename|parse params> <timer name> (<parent timer name>)");
        return;
    }

    var stats = LoadStats(statsFilename);
    if (stats == null)
    {
        arguments.RollBack();
        stats = Process(arguments);
    }

    if (stats == null)
    {
        Console.WriteLine("Failed to load stats");
        return;
    }

    var timerName = arguments.Argument();
    if (timerName == null)
    {
        Console.WriteLine("Params required: <stats filename|parse params> <timer name> (<parent timer name>)");
        return;
    }

    var timersMap = LoadTimersMap();

    if (!timersMap.TryGetId(timerName, out var id))
    {
        Console.WriteLine("Timer not found");
        return;
    }

    var parentName = arguments.Argument();
    if (parentName == null)
    {
        var stat = stats.Get(id);
        if (stat == null)
        {
            Console.WriteLine("Stat not found");
            return;
        }

        Console.WriteLine($"\n{stat}");
    }
    else
    {
        if (!timersMap.TryGetId(parentName, out var parentId))
        {
            Console.WriteLine("Parent timer not found");
            return;
        }

        var stat = stats.Get(id, parentId);
        if (stat == null)
        {
            Console.WriteLine("Stat not found");
            return;
        }

        Console.WriteLine($"\n{stat}");
    }
}

var arguments = new Arguments(args);
var program = arguments.Argument();
if (program == "process")
{
    Process(arguments);
}
else if (program == "compare")
{
    Compare(arguments);
}
else if (program == "analyze")
{
    Analyze(arguments);
}
else if (program == "stat")
{
    PrintStat(arguments);
}