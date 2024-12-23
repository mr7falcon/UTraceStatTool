namespace UTraceStatTool
{
    internal struct FrameEvent
    {
        public long TimerId;
        public float StartTime;
        public float EndTime;
    }

    internal class FramesTree
    {
        public FramesTree(in IEnumerable<TimingEvent> timingEvents, in Timer[] timers, in TimersMap timersMap)
        {
            var frameTimerId = Array.FindIndex(timers, t => t.Name == "FEngineLoop::Tick");
            if (frameTimerId == -1)
            {
                return;
            }

            var idMapping = GenerateIdMapping(timers, timersMap);
            MaxNumStats = timersMap.Size;

            using var logger = new ScopedLogger("Building frames tree");

            var iterator = timingEvents.GetEnumerator();
            if (!iterator.MoveNext())
            {
                return;
            }

            while (FindNextFrame(iterator, frameTimerId))
            {
                BuildFrame(iterator, idMapping);
            }
        }

        private static bool FindNextFrame(in IEnumerator<TimingEvent> iterator, long frameTimerId)
        {
            if (iterator.Current == null)
            {
                return false;
            }

            while (iterator.Current.TimerId != frameTimerId || float.IsInfinity(iterator.Current.StartTime) || float.IsInfinity(iterator.Current.EndTime))
            {
                if (!iterator.MoveNext())
                {
                    return false;
                }
            }

            return true;
        }

        private void BuildFrame(in IEnumerator<TimingEvent> iterator, in long[] idMapping)
        {
            var node = new FrameNode(null, iterator.Current, idMapping[iterator.Current.TimerId]);
            Frames.Add(node);

            var initialDepth = iterator.Current.Depth;

            while (iterator.MoveNext() && iterator.Current.Depth > initialDepth)
            {
                while (node.Depth >= iterator.Current.Depth)
                {
                    node = node.Parent;
                }
                
                if (iterator.Current.TimerId >= idMapping.Length || float.IsInfinity(iterator.Current.StartTime) || float.IsInfinity(iterator.Current.EndTime))
                {
                    continue;
                }

                node.Children.Add(new FrameNode(node, iterator.Current, idMapping[iterator.Current.TimerId]));
                node = node.Children.Last();
            }
        }

        private long[] GenerateIdMapping(in Timer[] timers, in TimersMap timersMap)
        {
            var idMapping = new long[timers.Length];
            foreach (var timer in timers)
            {
                idMapping[timer.Id] = timersMap.GetId(timer.Name);
            }
            return idMapping;
        }
        
        private IEnumerable<FrameNode> TraverseTree(FrameNode node)
        {
            yield return node;

            foreach (var childNode in node.Children.SelectMany(TraverseTree))
            {
                yield return childNode;
            }
        }

        public class FrameNode
        {
            public FrameNode(in FrameNode? parent, in TimingEvent timingEvent, long globalTimerId)
            {
                Event.TimerId = globalTimerId;
                Event.StartTime = timingEvent.StartTime;
                Event.EndTime = timingEvent.EndTime;
                Depth = timingEvent.Depth;
                Parent = parent;
            }

            public FrameEvent Event;
            public int Depth;
            public FrameNode? Parent;
            public List<FrameNode> Children = new();
        }
        
        public readonly List<FrameNode> Frames = new();
        public readonly long MaxNumStats;
        public IEnumerable<FrameNode> Nodes => Frames.SelectMany(TraverseTree);
    }
}
