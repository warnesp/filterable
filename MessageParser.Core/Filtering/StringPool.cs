using System;
using System.Collections.Concurrent;

namespace MessageParser.Core.Filtering
{
    public class StringPool
    {
        private readonly ConcurrentDictionary<string, string> _pool = new(StringComparer.Ordinal);

        public string GetOrAdd(string? val)
        {
            if (val == null) return string.Empty;
            return _pool.GetOrAdd(val, val);
        }
    }
}
