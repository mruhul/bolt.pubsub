using System;
using System.Collections.Generic;

namespace Bolt.PubSub
{
    public record Message
    {
        public Guid? Id { get; init; }
        public string AppId { get; init; }
        public string Tenant { get; init; }
        public string CorrelationId { get; init; }
        public int Version { get; init; }
        public string Type { get; init; }
        public DateTime? CreatedAt { get; init; }
        public Dictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    }

    public record Message<T> : Message
    {
        public T Content { get; init; }
    }
}
