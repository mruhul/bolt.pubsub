using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bolt.PubSub.RabbitMq.Subscribers;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Bolt.PubSub.RabbitMq.Tests
{
    public class MessageProcessor_Process_Should : TestWithIoc
    {
        public MessageProcessor_Process_Should(IocFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task Publish_to_error_queue_when_handler_not_available()
        {
            var sut = GetService<MessageProcessor>();

            var fakeChannel = Substitute.For<IModel>();

            var basicProperties = Substitute.For<IBasicProperties>();
            var basicDeliveryEventArgs = new BasicDeliverEventArgs
            {
                BasicProperties = basicProperties
            };
            var queueSetting = new QueueSettings
            {
                ErrorExchangeName = "error-exchange"
            };

            await sut.Process(fakeChannel, basicDeliveryEventArgs, queueSetting);

            fakeChannel.Received(1).BasicPublish(
                queueSetting.ErrorExchangeName,
                string.Empty,
                basicDeliveryEventArgs.BasicProperties,
                Arg.Any<ReadOnlyMemory<byte>>());

        }
    }
}
