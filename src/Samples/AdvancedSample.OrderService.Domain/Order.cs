using AdvancedSample.Core.Domain;

namespace AdvancedSample.OrderService.Domain
{
	public enum OrderStatus
	{
		Waiting,
		Success,
		Failed
	}

	public class Order : Entity
	{
		public int ProductId { get; set; }
		public int Quantity { get; set; }
		public OrderStatus Status { get; set; }
	}
}