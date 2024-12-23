using MessagePack;

namespace UTraceStatTool
{
    [MessagePackObject(AllowPrivate = true)]
    internal partial class TimersMap : IMessagePackSerializationCallbackReceiver
    {
        public long GetId(string name)
        {
            if (_nameToId.TryGetValue(name, out var id))
            {
                return id;
            }

            id = _idToName.Count;

            _idToName.Add(name);
            _nameToId.Add(name, id);

            return id;
        }

        public bool TryGetId(string name, out long id)
        {
            if (_nameToId.TryGetValue(name, out var fnd))
            {
                id = fnd;
                return true;
            }

            id = -1;
            return false;
        }

        public string GetName(long id)
        {
            if (id < 0 || id >= _idToName.Count)
            {
                return "";
            }
            return _idToName[(int)id];
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            for (var i = 0; i < _idToName.Count; i++)
            {
                _nameToId[_idToName[i]] = i;
            }
        }
        
        [IgnoreMember]
        public long Size => _idToName.Count;
        
        [Key(0)]
        private List<string> _idToName = new();
        
        [IgnoreMember]
        private readonly Dictionary<string, int> _nameToId = new();
    }
}
