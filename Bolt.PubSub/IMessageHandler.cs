using System;
using System.Threading.Tasks;

namespace Bolt.PubSub
{
    public interface IMessageHandler
    {
        Task<HandlerResponse> Handle(Message msg, ReadOnlySpan<byte> content, IMessageSerializer serializer);
        bool IsApplicable(string queueName, Message msg);
    }

    public record HandlerResponse
    {
        public HandlerStatusCode Status { get; init; }
        public string StatusReason { get; init; }

        public static implicit operator HandlerResponse(HandlerStatusCode code) => new HandlerResponse
        {
            Status = code
        };
    }

    public enum HandlerStatusCode
    {
        Success,
        TransientError,
        FatalError
    }

    public abstract class MessageHandler<T> : IMessageHandler
    {
        protected abstract Task<HandlerResponse> Handle(Message<T> msg);

        public Task<HandlerResponse> Handle(Message msg, ReadOnlySpan<byte> content, IMessageSerializer serializer)
        {
            var body = serializer.Deserialize<T>(content);

            return Handle(new Message<T>
            {
                AppId = msg.AppId,
                CorrelationId = msg.CorrelationId,
                CreatedAt = msg.CreatedAt,
                Headers = msg.Headers,
                Id = msg.Id,
                Tenant = msg.Tenant,
                Type = msg.Type,
                Version = msg.Version,
                Content = body,
            });
        }

        /// <summary>
        /// Provide the queuename that you like to scope your handler to listen for message. Set empty or null if you
        /// don't need any scope for the message to a specific queue.
        /// </summary>
        protected abstract string LinkedQueueName { get; }

        public virtual bool IsApplicable(string queueName, Message msg)
            => (string.IsNullOrWhiteSpace(LinkedQueueName) || string.Equals(LinkedQueueName, queueName, StringComparison.OrdinalIgnoreCase)) 
            && string.Equals(msg.Type, MessageTypeNameProvider.Get<T>(), StringComparison.OrdinalIgnoreCase);
    }
}
