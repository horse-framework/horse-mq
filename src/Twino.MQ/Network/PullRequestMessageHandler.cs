using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Options;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class PullRequestMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly TwinoMQ _server;

        public PullRequestMessageHandler(TwinoMQ server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TwinoMessage message, bool fromNode)
        {
            TwinoQueue queue = _server.FindQueue(message.Target);

            //if auto creation active, try to create queue
            if (queue == null && _server.Options.AutoQueueCreation)
            {
                QueueOptions options = QueueOptions.CloneFrom(_server.Options);
                queue = await _server.CreateQueue(message.Target, options, message, _server.DeliveryHandlerFactory);
            }

            if (queue == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            await HandlePullRequest(client, message, queue);
        }


        /// <summary>
        /// Handles pulling a message from a queue
        /// </summary>
        private async Task HandlePullRequest(MqClient client, TwinoMessage message, TwinoQueue queue)
        {
            //only pull statused queues can handle this request
            if (queue.Status != QueueStatus.Pull)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(MessageBuilder.CreateNoContentPullResponse(message, TwinoHeaders.UNACCEPTABLE));
                
                return;
            }

            //client cannot pull message from the queue not in
            QueueClient queueClient = queue.FindClient(client);
            if (queueClient == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(MessageBuilder.CreateNoContentPullResponse(message, TwinoHeaders.UNACCEPTABLE));
                
                return;
            }

            //check authorization
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanPullFromQueue(queueClient, queue);
                if (!grant)
                {
                    if (!string.IsNullOrEmpty(message.MessageId))
                        await client.SendAsync(MessageBuilder.CreateNoContentPullResponse(message, TwinoHeaders.UNAUTHORIZED));
                    return;
                }
            }

            await queue.State.Pull(queueClient, message);
        }
    }
}