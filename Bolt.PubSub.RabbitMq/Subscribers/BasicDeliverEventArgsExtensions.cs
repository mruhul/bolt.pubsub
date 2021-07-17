using RabbitMQ.Client;
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
    }
}
