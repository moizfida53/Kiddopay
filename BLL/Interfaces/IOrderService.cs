using Kiddopay.BLL.DTOs;

namespace Kiddopay.BLL.Interfaces
{
    public interface IOrderService
    {
        /// <summary>
        /// Validates the cart, deducts from the student's wallet, creates the
        /// blser_order + blser_orderline records, and updates pre-order status
        /// if applicable. Returns the completed order summary.
        /// </summary>
        OrderResultDTO CompleteOrder(CompleteOrderRequest request);

        /// <summary>
        /// Cancels an in-progress order before it is completed.
        /// Does not deduct from the wallet. Marks lines as RemovedManually.
        /// </summary>
        bool CancelOrder(Guid orderId);
    }
}
