using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct
{
    internal class RequestHandlerExecuter<TRequest, TResponse> : ExecuterBase
    {
        private readonly Type _handlerType;
        private readonly IHorseRequestHandler<TRequest, TResponse> _handler;
        private readonly Func<IHandlerFactory> _handlerFactoryCreator;

        public RequestHandlerExecuter(Type handlerType, IHorseRequestHandler<TRequest, TResponse> handler, Func<IHandlerFactory> handlerFactoryCreator)
        {
            _handlerType = handlerType;
            _handler = handler;
            _handlerFactoryCreator = handlerFactoryCreator;
        }

        public override void Resolve(object registration)
        {
            DirectHandlerRegistration reg = registration as DirectHandlerRegistration;
            // TODO : Emre | reg null olma ihtimali var mı? 
            ResolveAttributes(reg!.ConsumerType);
        }
        
        public override async Task Execute(HorseClient client, HorseMessage message, object model)
        {
            bool respond = false;
            ProvidedHandler providedHandler = null;
            IHandlerFactory handlerFactory = null;
            
            try
            {
                TRequest requestModel = (TRequest) model;
                IHorseRequestHandler<TRequest, TResponse> handler;

                if (_handler != null) handler = _handler;
                else if (_handlerFactoryCreator != null)
                {
                    handlerFactory = _handlerFactoryCreator();
                    providedHandler = handlerFactory.CreateHandler(_handlerType);
                    handler = (IHorseRequestHandler<TRequest, TResponse>) providedHandler.Service;
                }
                else
                    throw new NullReferenceException("There is no consumer defined");

                try
                {
                    await RunBeforeInterceptors(message, client, handlerFactory);
                    TResponse responseModel = await Handle(handler, requestModel, message, client);
                    HorseResultCode code = responseModel is null ? HorseResultCode.NoContent : HorseResultCode.Ok;
                    HorseMessage responseMessage = message.CreateResponse(code);

                    if (responseModel != null)
                        responseMessage.Serialize(responseModel, client.MessageSerializer);

                    respond = true;
                    await client.SendAsync(responseMessage);
                    await RunAfterInterceptors(message, client, handlerFactory);
                }
                catch (Exception e)
                {
                    ErrorResponse errorModel = await handler.OnError(e, requestModel, message, client);

                    if (errorModel.ResultCode == HorseResultCode.Ok)
                        errorModel.ResultCode = HorseResultCode.Failed;

                    HorseMessage responseMessage = message.CreateResponse(errorModel.ResultCode);

                    if (!string.IsNullOrEmpty(errorModel.Reason))
                        responseMessage.SetStringContent(errorModel.Reason);

                    respond = true;
                    await client.SendAsync(responseMessage);
                    throw;
                }
            }
            catch (Exception e)
            {
                if (!respond)
                {
                    try
                    {
                        HorseMessage response = message.CreateResponse(HorseResultCode.InternalServerError);
                        await client.SendAsync(response);
                    }
                    catch
                    {
                        // Ignored. Because we cant return response. Client will be timeout. Maybe we can retry mechanism to here.
                    }
                }

                await SendExceptions(message, client, e);
            }
            finally
            {
                providedHandler?.Dispose();
            }
        }

        private async Task<TResponse> Handle(IHorseRequestHandler<TRequest, TResponse> handler, TRequest request, HorseMessage message, HorseClient client)
        {
            if (Retry == null)
                return await handler.Handle(request, message, client);

            int count = Retry.Count == 0 ? 100 : Retry.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    return await handler.Handle(request, message, client);
                }
                catch (Exception e)
                {
                    Type type = e.GetType();
                    if (Retry.IgnoreExceptions is { Length: > 0 })
                    {
                        if (Retry.IgnoreExceptions.Any(x => x.IsAssignableFrom(type)))
                            throw;
                    }

                    if (Retry.DelayBetweenRetries > 0)
                        await Task.Delay(Retry.DelayBetweenRetries);
                }
            }

            throw new OperationCanceledException("Reached to maximum retry count and execution could not be completed");
        }
    }
}