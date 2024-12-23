namespace UTraceStatTool
{
    //var specialTimers = new [] { "ConditionalCollectGarbage", "FlushAsyncLoading", "WaitForTasks" };
    
    internal class AnalysisHelpers
    {
        private class ResultStream : IDisposable
        {
            public ResultStream(bool bDump, int show)
            {
                _show = show;
                if (bDump)
                {
                    _writer = new("results.txt");
                }
            }

            public void Dispose()
            {
                _writer?.Dispose();
                _writer = null;
            }

            public bool Add(string message)
            {
                if (_show > 0)
                {
                    Console.WriteLine("\n" + message);
                    --_show;
                }

                _writer?.WriteLine("\n" + message);

                return _show > 0 || _writer != null;
            }

            private int _show;
            private StreamWriter? _writer;
        }

        public static void AnalyzeFrames(in IEnumerable<(int, FramesTree.FrameNode)> frames, StatsContainer stats, TimersMap timersMap, bool bDump, int show)
        {
            var exceptions = new FrameAnalyzer.ExceptionContainer();
            
            {
                using var logger = new ScopedLogger("Analyzing frames");

                foreach (var analyzer in frames.Select(f => new FrameAnalyzer(f.Item1, f.Item2, stats, exceptions)))
                {
                }
            }
            
            using var results = new ResultStream(bDump, show);

            foreach (var (key, value) in exceptions.Exceptions.OrderByDescending(e => e.Value.Deviation))
            {
                if (!results.Add($"{timersMap.GetName(key.Id)}({timersMap.GetName(key.ParentId)})\n" +
                                 $"{key.Exception}: {value.Events.Count} events, Deviation {value.Deviation}"))
                {
                    break;
                }
            }
        }
        
        public static void CompareStats(in StatsContainer stable, in StatsContainer tested, in TimersMap timersMap, bool bDump, int show)
        {
            static string DiffString(string name, float diff1, float value1, float value2, float diff2)
            {
                static string Vwpip(float value) => value > 0f ? $"+{value}" : value.ToString();
                return $"{name} ({Vwpip(diff1)}): {value1} -> {value2} ({Vwpip(diff2)})\n";
            }

            using var results = new ResultStream(bDump, show);

            foreach (var (id, parentId, description, stableStat, testedStat, diff) in StatsContainer.Compare(stable, tested).OrderByDescending(c => c.Item6.Determinant))
            {
                var message = parentId == -1
                    ? $"{timersMap.GetName(id)}, {description}:\n"
                    : $"{timersMap.GetName(id)}({timersMap.GetName(parentId)}), {description}:\n";
                message += DiffString("Mean", diff.Mean.Item1, stableStat.Mean, testedStat.Mean, diff.Mean.Item2);
                message += DiffString("Variance", diff.Variance.Item1, stableStat.Variance, testedStat.Variance, diff.Variance.Item2);
                message += DiffString("Standard deviation", diff.StandardDeviation.Item1, stableStat.StandardDeviation, testedStat.StandardDeviation, diff.StandardDeviation.Item2);
                message += DiffString("Median", diff.Median.Item1, stableStat.Median, testedStat.Median, diff.Median.Item2);

                if (!results.Add(message))
                {
                    break;
                }
            }
        }
    }
}
