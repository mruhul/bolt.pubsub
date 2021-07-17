using System;

namespace Bolt.PubSub
{
    public class MessageTypeNameAttribute : Attribute
    {
        public MessageTypeNameAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
