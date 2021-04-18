using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Models;
using Horse.Mq.Client.Annotations;

namespace Test.Bus.Models
{
    [QueueName("ex-queue-2")]
    [RouterName("ex-route-2")]
    public class ExceptionModel2 : ITransportableException
    {
        public void Initialize(ExceptionContext context)
        {
        }
    }
}