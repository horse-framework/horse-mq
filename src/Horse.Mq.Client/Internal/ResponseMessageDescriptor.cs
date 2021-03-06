using System;
using System.Threading.Tasks;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Internal
{
    /// <summary>
    /// Followed response message descriptor 
    /// </summary>
    internal class ResponseMessageDescriptor : MessageDescriptor
    {
        public TaskCompletionSource<HorseMessage> Source { get; }

        public ResponseMessageDescriptor(HorseMessage message, DateTime expiration) : base(message, expiration)
        {
            Source = new TaskCompletionSource<HorseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <inheritdoc />
        public override void Set(bool successful, object value)
        {
            if (SourceCompleted)
                return;

            SourceCompleted = true;
            
            if (!successful || value == null)
                Source.SetResult(default);
            else
                Source.SetResult(value as HorseMessage);
        }
    }
}