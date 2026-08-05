using System;
using System.Collections.Generic;
using MessageParser.Core.Bus;
using MessageParser.Core.Messages;
using MessageParser.Plugins;
using Xunit;

namespace MessageParser.Tests
{
    public class DataBusTests
    {
        private class TestMessage : MessageBase
        {
            public string Payload { get; set; } = string.Empty;
        }

        private class DerivedTestMessage : TestMessage
        {
        }

        [Fact]
        public void Publish_DeliversToExactSubscribedType()
        {
            var bus = new DataBus();
            TestMessage? received = null;

            using (bus.Subscribe<TestMessage>(msg => received = msg))
            {
                var msg = new TestMessage { Sender = "A", Receiver = "B", Payload = "Hello" };
                bus.Publish(msg);

                Assert.NotNull(received);
                Assert.Equal("Hello", received.Payload);
            }
        }

        [Fact]
        public void Publish_PolymorphicInheritance_DeliversToBaseSubscribers()
        {
            var bus = new DataBus();
            MessageBase? baseReceived = null;
            TestMessage? testReceived = null;

            using (bus.Subscribe<MessageBase>(msg => baseReceived = msg))
            using (bus.Subscribe<TestMessage>(msg => testReceived = msg))
            {
                var derived = new DerivedTestMessage { Sender = "Node1", Receiver = "Node2", Payload = "Derived" };
                bus.Publish(derived);

                Assert.NotNull(baseReceived);
                Assert.NotNull(testReceived);
                Assert.Same(derived, baseReceived);
                Assert.Same(derived, testReceived);
            }
        }

        [Fact]
        public void Unsubscribe_StopsReceivingMessages()
        {
            var bus = new DataBus();
            int count = 0;

            var sub = bus.Subscribe<TestMessage>(_ => count++);
            bus.Publish(new TestMessage());
            Assert.Equal(1, count);

            sub.Dispose();
            bus.Publish(new TestMessage());
            Assert.Equal(1, count);
        }

        [Fact]
        public void UnhandledException_FiresEventWhenSubscriberFails()
        {
            var bus = new DataBus();
            DataBusExceptionEventArgs? errorArgs = null;

            bus.UnhandledException += (s, e) => errorArgs = e;

            bus.Subscribe<TestMessage>(_ => throw new InvalidOperationException("Subscriber error test"));

            var message = new TestMessage { Payload = "Fail" };
            bus.Publish(message);

            Assert.NotNull(errorArgs);
            Assert.IsType<InvalidOperationException>(errorArgs.Exception);
            Assert.Equal("Subscriber error test", errorArgs.Exception.Message);
            Assert.Same(message, errorArgs.Message);
            Assert.Equal(typeof(TestMessage), errorArgs.SubscribedType);
        }

        [Fact]
        public void Publish_HighVolumeDispatchCache_MaintainsCorrectness()
        {
            var bus = new DataBus();
            int receivedCount = 0;

            using (bus.Subscribe<HeartBeat>(_ => receivedCount++))
            {
                for (int i = 0; i < 10000; i++)
                {
                    bus.Publish(new HeartBeat());
                }

                Assert.Equal(10000, receivedCount);
            }
        }
    }
}
