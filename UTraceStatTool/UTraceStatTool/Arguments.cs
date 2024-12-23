namespace UTraceStatTool
{
    internal class Arguments
    {
        public Arguments(IEnumerable<string> args)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith('-') && arg.Length > 1)
                {
                    var res = arg[1..].Split('=');
                    if (res.Length > 1)
                    {
                        if (double.TryParse(res[1].TrimStart(), out var value))
                        {
                            _params[res[0].TrimEnd()] = value;
                        }
                        else
                        {
                            _params[res[0].TrimEnd()] = res[1].TrimStart();
                        }
                    }
                    else
                    {
                        _flags.Add(res[0].TrimEnd());
                    }
                }
                else
                {
                    _arguments.Add(arg);
                }
            }
        }

        public string? Argument(int pos = -1)
        {
            if (pos < 0)
            {
                pos = _nextArgPos++;
            }
            else
            {
                _nextArgPos = pos + 1;
            }
            return pos >= _arguments.Count ? null : _arguments[pos];
        }

        public void RollBack()
        {
            --_nextArgPos;
        }
        
        public bool Flag(string key)
        {
            return _flags.Contains(key);
        }

        public bool Param<T>(string key, out T value)
        {
            if (!_params.TryGetValue(key, out var param))
            {
                value = default;
                return false;
            }

            value = (T)Convert.ChangeType(param, typeof(T));
            return true;
        }

        private int _nextArgPos = 0;

        private readonly List<string> _arguments = new();
        private readonly HashSet<string> _flags = new();
        private readonly Dictionary<string, object> _params = new ();
    }
}
