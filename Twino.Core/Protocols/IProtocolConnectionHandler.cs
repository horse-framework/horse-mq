using System.Collections.Generic;
using System.Threading.Tasks;

namespace Twino.Core.Protocols
{
    /// <summary>
    /// Twino Protocol Connection handler implementation
    /// </summary>
    public interface IProtocolConnectionHandler<in TMessage>
    {
        /// <summary>
        /// Triggered when a piped client is connected. 
        /// </summary>
        Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data);

        /// <summary>
        /// Triggered when a client sends a message to the server 
        /// </summary>
        Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, TMessage message);

        /// <summary>
        /// Triggered when a piped client is disconnected. 
        /// </summary>
        Task Disconnected(ITwinoServer server, SocketBase client);
    }
}