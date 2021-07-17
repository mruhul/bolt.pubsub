using System.Collections.Concurrent;
using System.Reflection;

namespace Bolt.PubSub
{
    internal static class MessageTypeNameProvider
    {
        private static ConcurrentDictionary<string, string> Cached = new ConcurrentDictionary<string, string>();

        public static string Get<T>()
        {
            var type = typeof(T);
            var typeName = type.FullName;

            if(Cached.TryGetValue(typeName, out var value))
            {
                return value;
            }

            var attr = type.GetCustomAttribute<MessageTypeNameAttribute>();

            if(attr == null)
            {
                return Cached.GetOrAdd(typeName, type.Name);
            }
            else
            {
                return Cached.GetOrAdd(typeName, string.IsNullOrWhiteSpace(attr.Name) ? type.Name : attr.Name);
            }
        }
    }
}
