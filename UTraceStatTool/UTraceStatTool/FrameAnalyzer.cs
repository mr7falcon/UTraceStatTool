namespace UTraceStatTool
{
    internal class FrameAnalyzer
    {
        public FrameAnalyzer(long frameId, in FramesTree.FrameNode root, in StatsContainer stats, in ExceptionContainer? exceptions = null)
        {
            _frameId = frameId;
            _stats = stats;
            
            Exceptions = exceptions ?? new();

            var stat = _stats.Get(root.Event.TimerId);
            if (stat == null)
            {
                return;
            }

            RootDeviation = CalculateDeviation(root.Event.EndTime - root.Event.StartTime, stat.Value.InclDuration);
            CompensatedDeviation = TraverseChildren(root);
        }

        private float TraverseChildren(FramesTree.FrameNode node)
        {
            var compensatedDeviation = 0f;
            
            var medianNumRecords = GetMedianNumRecords(node);

            foreach (var group in node.Children
                         .GroupBy(c => c.Event.TimerId)
                         .Select(g => new
                         {
                             Key = g.Key,
                             Nodes = g,
                             Duration = g.Sum(n => n.Event.EndTime - n.Event.StartTime),
                             NumCalls = g.Count()
                         })
                         .OrderByDescending(g => g.Duration))
            {
                if (group.Duration < 1e-5)
                {
                    break;
                }

                var bRelevant = GetStats(group.Key, node.Event.TimerId, out var inclDurationStat, out var exclDurationStat, out var numCallsStat);
                if (bRelevant)
                {
                    if (numCallsStat != null && numCallsStat.Value.SampleCount < 0.1f * medianNumRecords)
                    {
                        Exceptions.Add(ExceptionContainer.RareEventException, _frameId, group.Key, node.Event.TimerId,
                            group.Duration, group.Nodes.First().Event.StartTime, group.Nodes.Last().Event.EndTime);
                        compensatedDeviation += group.Duration;
                        //continue;
                    }

                    if (numCallsStat != null)
                    {
                        var numCallsDeviation = CalculateDeviation(group.NumCalls, numCallsStat.Value);
                        if (numCallsDeviation > 0)
                        {
                            var deviation = numCallsDeviation * inclDurationStat.Mean;
                            Exceptions.Add(ExceptionContainer.NumCallsException, _frameId, group.Key, node.Event.TimerId,
                                deviation, group.Nodes.First().Event.StartTime, group.Nodes.Last().Event.EndTime);
                            compensatedDeviation += deviation;
                            //continue;
                        }
                    }
                }

                foreach (var child in group.Nodes)
                {
                    if (bRelevant)
                    {
                        var inclDuration = child.Event.EndTime - child.Event.StartTime;
                        var exclDuration = inclDuration - child.Children.Sum(c => c.Event.EndTime - c.Event.StartTime);
                        var exclDurationDeviation = CalculateDeviation(exclDuration, exclDurationStat);
                        if (exclDurationDeviation > 0)
                        {
                            Exceptions.Add(ExceptionContainer.ExcludingDurationException, _frameId, child.Event.TimerId,
                                node.Event.TimerId, exclDurationDeviation, child.Event.StartTime, child.Event.EndTime);
                            compensatedDeviation += exclDurationDeviation;

                            if (exclDurationDeviation >= CalculateDeviation(inclDuration, inclDurationStat))
                            {
                                //continue;
                            }
                        }
                    }

                    compensatedDeviation += TraverseChildren(child);
                }
            }

            return compensatedDeviation;
        }

        private bool GetStats(long id, long parentId, out StatsContainer.Stat inclDurationStat, out StatsContainer.Stat exclDurationStat, out StatsContainer.Stat? numCallsStat)
        {
            static bool IsRelevant(StatsContainer.Stat stat) => stat.SampleCount > 10;

            var branchStats = _stats.Get(id, parentId);
            if (branchStats != null && IsRelevant(branchStats.Value.NumCalls))
            {
                inclDurationStat = branchStats.Value.CommonStats.InclDuration;
                exclDurationStat = branchStats.Value.CommonStats.ExclDuration;
                numCallsStat = branchStats.Value.NumCalls;
                return true;
            }

            numCallsStat = null;

            var commonStats = _stats.Get(id);
            if (commonStats != null && IsRelevant(commonStats.Value.InclDuration))
            {
                inclDurationStat = commonStats.Value.InclDuration;
                exclDurationStat = commonStats.Value.ExclDuration;
                return true;
            }

            inclDurationStat = new();
            exclDurationStat = new();

            return false;
        }

        private float GetMedianNumRecords(FramesTree.FrameNode node)
        {
            var numRecords = node.Children
                .DistinctBy(c => c.Event.TimerId)
                .Select(c => _stats.Get(c.Event.TimerId, node.Event.TimerId))
                .Select(s => s != null ? s.Value.NumCalls.SampleCount : 0)
                .Order()
                .ToList();

            if (numRecords.Count == 0)
            {
                return 0;
            }

            var mid = numRecords.Count / 2;
            return numRecords.Count % 2 == 0 ? 0.5f * (numRecords[mid] - numRecords[mid - 1]) : numRecords[mid];
        }
        
        private static float CalculateDeviation(float value, StatsContainer.Stat stat)
        {
            return MathF.Max(0, value - (stat.Mean + stat.StandardDeviation));
        }

        internal class ExceptionContainer
        {
            public static string RareEventException = "Rare event in the context";
            public static string NumCallsException = "Enormous number of occurancies of the event";
            public static string ExcludingDurationException = "Enormous event excluding duration";
            
            internal struct Event
            {
                public long FrameId;
                public float StartTime;
                public float EndTime;
                public float Deviation;
            }

            internal struct Key
            {
                public string Exception;
                public long Id;
                public long ParentId;
            }

            internal class Value
            {
                public void Add(Event evt)
                {
                    Events.Add(evt);
                    Deviation += evt.Deviation;
                }

                public float Deviation;
                public List<Event> Events = new();
            }
            
            public void Add(string exception, long frameId, long id, long parentId, float deviation, float startTime, float endTime)
            {
                var key = new Key { Exception = exception, Id = id, ParentId = parentId };
                var evt = new Event { FrameId = frameId, StartTime = startTime, EndTime = endTime, Deviation = deviation };

                if (Exceptions.TryGetValue(key, out var value))
                {
                    value.Add(evt);
                }
                else
                {
                    value = new();
                    value.Add(evt);
                    Exceptions.Add(key, value);
                }
            }
            
            public Dictionary<Key, Value> Exceptions = new();
        }
        
        public readonly float RootDeviation;
        public readonly float CompensatedDeviation;
        public readonly ExceptionContainer Exceptions;

        private readonly long _frameId;
        private readonly StatsContainer _stats;
    }
}
