namespace UTraceStatTool
{
    internal class Timer
    {
        public long Id { get; set; }
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public string File { get; set; } = "";
        public int Line { get; set; }

        public override string ToString()
        {
            return $"Id={Id}, Type={Type}, Name={Name}, File={File}, Line={Line}";
        }
    }
}
