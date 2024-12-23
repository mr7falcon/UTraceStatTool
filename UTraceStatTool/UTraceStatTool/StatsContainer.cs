using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using MessagePack;

namespace UTraceStatTool
{
    [MessagePackObject(AllowPrivate = true)]
    internal partial class StatsContainer
    {
        [MessagePackObject(AllowPrivate = true)]
        public struct Stats
        {
            public Stats()
            {
                InclDuration = new();
                ExclDuration = new();
            }

            [Description("Including duration")] 
            [Key(0)]
            public Stat InclDuration;

            [Description("Excluding duration")] 
            [Key(1)]
            public Stat ExclDuration;

            public static IEnumerable<(string, Stat, Stat, StatDiff)> Compare(Stats lhs, Stats rhs)
            {
                yield return (RetrieveDescription<Stats>(nameof(InclDuration)), lhs.InclDuration, rhs.InclDuration, Stat.Compare(lhs.InclDuration, rhs.InclDuration));
                yield return (RetrieveDescription<Stats>(nameof(ExclDuration)), lhs.ExclDuration, rhs.ExclDuration, Stat.Compare(lhs.ExclDuration, rhs.ExclDuration));
            }

            public readonly override string ToString()
            {
                return "Including duration:\n" +
                       $"{InclDuration}\n\n" +
                       "Excluding duration:\n" +
                       $"{ExclDuration}\n";
            }
        }

        [MessagePackObject(AllowPrivate = true)]
        public struct BranchStats
        {
            public BranchStats()
            {
                CommonStats = new();
                NumCalls = new();
            }

            [Key(0)]
            public Stats CommonStats;

            [Description("Number of calls")] 
            [Key(1)]
            public Stat NumCalls;

            public static IEnumerable<(string, Stat, Stat, StatDiff)> Compare(BranchStats lhs, BranchStats rhs)
            {
                foreach (var cmp in Stats.Compare(lhs.CommonStats, rhs.CommonStats))
                {
                    yield return cmp;
                }
                yield return (RetrieveDescription<BranchStats>(nameof(NumCalls)), lhs.NumCalls, rhs.NumCalls, Stat.Compare(lhs.NumCalls, rhs.NumCalls));
            }

            public readonly override string ToString()
            {
                return $"{CommonStats}\n" +
                       "Number of calls:\n" +
                       $"{NumCalls}\n";
            }
        }

        private static string RetrieveDescription<T>(string name)
        {
            return typeof(T).GetField(name).GetCustomAttribute<DescriptionAttribute>().Description;
        }

        [MessagePackObject(AllowPrivate = true)]
        public partial struct Stat
        {
            public Stat()
            {
                _values = new();
            }

            public readonly void Add(float value)
            {
                _values.Add(value);
            }

            public void Derive()
            {
                if (_values.Count <= 0)
                {
                    return;
                }

                _values.Sort();

                Mean = _values.Sum() / _values.Count;

                var mean = Mean;
                Variance = _values.Sum(v => (v - mean) * (v - mean)) / _values.Count;
                StandardDeviation = MathF.Sqrt(Variance);

                var mid = _values.Count / 2;
                Median = _values.Count % 2 == 0 ? 0.5f * (_values[mid] + _values[mid - 1]) : _values[mid];
            }

            public static StatDiff Compare(in Stat lhs, in Stat rhs)
            {
                Tuple<float, float> GetDiff(float val1, float val2)
                {
                    var diff = val2 - val1;
                    var port = 0f;
                    if (val1 != 0f)
                    {
                        port = diff / val1;
                    }
                    else if (val2 != 0f)
                    {
                        port = 1f;
                    }
                    return new Tuple<float, float>(port, diff);
                }

                return new ()
                {
                    Mean = GetDiff(lhs.Mean, rhs.Mean),
                    Variance = GetDiff(lhs.Variance, rhs.Variance),
                    StandardDeviation = GetDiff(lhs.StandardDeviation, rhs.StandardDeviation),
                    Median = GetDiff(lhs.Median, rhs.Median)
                };
            }

            public readonly override string ToString()
            {
                return $"Mean {Mean} Variance {Variance} StandardDeviation {StandardDeviation} Median {Median} SampleCount {SampleCount}\n";
            }

            [Key(0)]
            public float Mean;
            
            [Key(1)]
            public float Variance;
            
            [Key(2)]
            public float StandardDeviation;
            
            [Key(3)]
            public float Median;

            [IgnoreMember] 
            public readonly int SampleCount => _values.Count;

            [Key(4)]
            private List<float> _values;
        }

        public struct StatDiff
        {
            public Tuple<float, float> Mean;
            public Tuple<float, float> Variance;
            public Tuple<float, float> StandardDeviation;
            public Tuple<float, float> Median;

            public readonly float Determinant => Mean.Item1 * Mean.Item2 + Variance.Item1 * Variance.Item2 + StandardDeviation.Item1 * StandardDeviation.Item2 + Median.Item1 * Median.Item2;
        }

        public static IEnumerable<(long, long, string, Stat, Stat, StatDiff)> Compare(StatsContainer lhs, StatsContainer rhs)
        {
            using var logger = new ScopedLogger("Comparing stats");

            for (var i = 0; i < Math.Min(lhs._stats.Length, rhs._stats.Length); i++)
            {
                var entry1 = i < lhs._stats.Length ? lhs._stats[i] : new();
                var entry2 = i < rhs._stats.Length ? rhs._stats[i] : new();

                foreach (var cmp in Stats.Compare(entry1.Common, entry2.Common))
                {
                    yield return (i, -1, cmp.Item1, cmp.Item2, cmp.Item3, cmp.Item4);
                }

                foreach (var j in entry1.Branch.Keys.Concat(entry2.Branch.Keys).Distinct())
                {
                    entry1.Branch.TryGetValue(j, out var branch1);
                    entry2.Branch.TryGetValue(j, out var branch2);

                    foreach (var cmp in BranchStats.Compare(branch1, branch2))
                    {
                        yield return (i, j, cmp.Item1, cmp.Item2, cmp.Item3, cmp.Item4);
                    }
                }
            }
        }
        
        public void DeriveStats(in FramesTree framesTree)
        {
            using var derivingStatsLogger = new ScopedLogger("Deriving stats");

            EnsureSize(framesTree.MaxNumStats);

            foreach (var node in framesTree.Nodes)
            {
                var inclDuration = node.Event.EndTime - node.Event.StartTime;
                var exclDuration = inclDuration - node.Children.Sum(c => c.Event.EndTime - c.Event.StartTime);
                inclDuration *= 1000f;
                exclDuration *= 1000f;

                ref var commonStats = ref GetCommonStats(node.Event.TimerId);
                commonStats.InclDuration.Add(inclDuration);
                commonStats.ExclDuration.Add(exclDuration);

                if (node.Parent != null)
                {
                    ref var branchStats = ref GetBranchStats(node.Event.TimerId, node.Parent.Event.TimerId);
                    branchStats.CommonStats.InclDuration.Add(inclDuration);
                    branchStats.CommonStats.ExclDuration.Add(exclDuration);
                }

                foreach (var group in node.Children.GroupBy(c => c.Event.TimerId))
                {
                    ref var branchStats = ref GetBranchStats(group.Key, node.Event.TimerId);
                    branchStats.NumCalls.Add(group.Count());
                }
            }

            for (var i = 0; i < _stats.Length; i++)
            {
                _stats[i].Common.InclDuration.Derive();
                _stats[i].Common.ExclDuration.Derive();

                foreach (var parentId in _stats[i].Branch.Keys)
                {
                    ref var branchStats = ref CollectionsMarshal.GetValueRefOrNullRef(_stats[i].Branch, parentId);
                    branchStats.CommonStats.InclDuration.Derive();
                    branchStats.CommonStats.ExclDuration.Derive();
                    branchStats.NumCalls.Derive();
                }
            }
        }

        public Stats? Get(long id)
        {
            if (id < 0 || id >= _stats.Length)
            {
                return null;
            }

            return _stats[id].Common;
        }

        public BranchStats? Get(long id, long parentId)
        {
            if (id < 0 || id >= _stats.Length)
            {
                return null;
            }

            if (_stats[id].Branch.TryGetValue(parentId, out var value))
            {
                return value;
            }

            return null;
        }
        
        private ref BranchStats GetBranchStats(long id, long parentId)
        {
            ref var stats = ref CollectionsMarshal.GetValueRefOrAddDefault(_stats[id].Branch, parentId, out var exists);
            if (!exists)
            {
                stats = new();
            }
            return ref stats;
        }

        private ref Stats GetCommonStats(long id)
        {
            return ref _stats[id].Common;
        }

        private void EnsureSize(long size)
        {
            if (_stats.Length < size)
            {
                var statsNew = new StatsEntry[size];
                Array.Copy(_stats, statsNew, _stats.Length);

                for (var i = _stats.Length; i < statsNew.Length; i++)
                {
                    statsNew[i] = new();
                }

                _stats = statsNew;
            }
        }
        
        [MessagePackObject(AllowPrivate = true)]
        internal struct StatsEntry
        {
            public StatsEntry()
            {
                Common = new();
                Branch = new();
            }

            [Key(0)]
            public Stats Common;

            [Key(1)]
            public Dictionary<long, BranchStats> Branch;
        }

        [Key(0)]
        private StatsEntry[] _stats = Array.Empty<StatsEntry>();
    }
}
