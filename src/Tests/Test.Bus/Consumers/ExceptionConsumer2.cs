using System.Threading.Tasks;
using Test.Bus.Models;
using Horse.Mq.Client;
using Horse.Protocols.Hmq;

namespace Test.Bus.Consumers
{
    public class ExceptionConsumer2 : IQueueConsumer<ExceptionModel2>
    {
        public int Count { get; private set; }

        public static ExceptionConsumer2 Instance { get; private set; }

        public ExceptionConsumer2()
        {
            Instance = this;
        }

        public Task Consume(HorseMessage message, ExceptionModel2 model, HorseClient client)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}