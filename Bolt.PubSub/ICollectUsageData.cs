using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bolt.PubSub
{
    public interface ICollectUsageData
    {
        Task Notify(UsageData usageData);
    }

    public record UsageData
    {
        public string QueueName { get; init; }
        public string MessageType { get; init; }
        public string MessageId { get; init; }
        public Dictionary<string,object> Data { get; init; }
        public UsageDataType Type { get; init; }
    }

    public enum UsageDataType
    {
        MessageProcessed,
        MessageReceived,
        MessageTransientError,
        MessageFatalError
    }

    public static class UsageDataPropertyNames
    {
        public const string TimeTakenInMs = "TimeTakenInMs";
    }
}
