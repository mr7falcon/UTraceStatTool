using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace UTraceStatTool
{
    public class InfinitableFloatConverter : SingleConverter
    {
        public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData mapData)
        {
            return text is "inf" ? float.PositiveInfinity : base.ConvertFromString(text, row, mapData);
        }
    }

    internal class TimingEvent
    {
        public long ThreadId { get; set; }
        public long TimerId { get; set; }
        public float StartTime { get; set; }
        public float EndTime { get; set; }
        public int Depth { get; set; }

        public override string ToString()
        {
            return $"ThreadId={ThreadId}, TimerId={TimerId}, StartTime={StartTime}, EndTime={EndTime}, Depth={Depth}";
        }
    }
}
