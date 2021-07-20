namespace Bolt.PubSub
{
    public interface IMessageFilter
    {
        TMessage Filter<TMessage>(TMessage msg, string typeName) where TMessage : Message;
    }
}
