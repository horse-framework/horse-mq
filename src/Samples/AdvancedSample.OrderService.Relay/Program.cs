using System;
using System.Linq;
using System.Transactions;
using AdvancedSample.Common.Extensions;
using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.Core.Service;
using AdvancedSample.OrderService.Core;
using AdvancedSample.OrderService.Domain;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

IServiceCollection services = new ServiceCollection();
services.AddHorseBus(builder => { builder.BuildHorseClient("horse://localhost:15500", ClientTypes.ORDER_MESSAGE_RELAY); });
services.AddCoreServices();
IServiceProvider provider = services.BuildServiceProvider();
provider.UseHorseBus();
IHorseQueueBus bus = provider.GetRequiredService<IHorseQueueBus>();
while (true)
{
	using CommittableTransaction transaction = new(new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });
	await using DbContext context = provider.GetRequiredService<DbContext>();
	OutboxMessage relayMessage = await context.Set<OutboxMessage>()
											  .OrderBy(m => m.Id)
											  .FirstOrDefaultAsync(m => m.Status == OutboxMessageStatus.Waiting);
	if (relayMessage is null)
	{
		transaction.Commit();
		continue;
	}
	_ = Console.Out.WriteLineAsync($"RELAY | {relayMessage.MessageJSON}");
	try
	{
		relayMessage.Status = OutboxMessageStatus.Pending;
		context.SaveChanges();
		Type type = JsonConvert.DeserializeObject<Type>(relayMessage.Type);
		object? message = JsonConvert.DeserializeObject(relayMessage.MessageJSON, type);
		await bus.RaiseEvent(message);
		relayMessage.Status = OutboxMessageStatus.Sent;
		context.SaveChanges();
		transaction.Commit();
	}
	catch
	{
		transaction.Rollback();
	}
}