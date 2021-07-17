using RabbitMQ.Client;
using System.Collections.Generic;
using System.Text;

namespace Bolt.PubSub.RabbitMq.Subscribers
{
    internal static class BasicDeliverEventArgsExtensions
    {
        public static string TryReadHeader(this IBasicProperties src, string name)
        {
            var headers = src.Headers;

            if (headers == null) return null;

            if(headers.TryGetValue(name, out var value))
            {
                return Encoding.UTF8.GetString((byte[])value);
            }

            return null;
        }

        public static bool SetHeader(this IBasicProperties src, string name, string value)
        {
            if (value is null) return false;

            if (src.Headers == null) 
                src.Headers = new Dictionary<string, object>();

            src.Headers[name] = value;

            return true;
        }
    }
}
