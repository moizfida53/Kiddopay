using Kiddopay.BLL.DTOs;
using Kiddopay.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace KiddoPay.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController(IOrderService orderService) : ControllerBase
    {
        private readonly IOrderService _orders = orderService;

        /// <summary>
        /// Completes an order from the cashier screen.
        /// Handles three scenarios:
        ///   1. DirectScan     — no pre-order, cashier scanned fresh items.
        ///   2. PreOrderFulfillment — student had a pre-order, all items from it.
        ///   3. Mixed          — pre-order partially fulfilled + extra items scanned.
        ///
        /// On success: deducts wallet, records order + lines, updates pre-order status.
        /// </summary>
        [HttpPost("complete")]
        public IActionResult CompleteOrder([FromBody] CompleteOrderRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { message = "Request body is required." });

                if (request.StudentId == Guid.Empty)
                    return BadRequest(new { message = "StudentId is required." });

                if (request.CashierId == Guid.Empty)
                    return BadRequest(new { message = "CashierId is required." });

                if (request.StoreId == Guid.Empty)
                    return BadRequest(new { message = "StoreId is required." });

                if (request.Lines == null || !request.Lines.Any())
                    return BadRequest(new { message = "Order must contain at least one item." });

                var result = _orders.CompleteOrder(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                // Business rule violation (insufficient balance, empty cart, etc.)
                return UnprocessableEntity(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Cancels an in-progress order before it is completed.
        /// Called when the cashier taps "Cancel Order" on the cashier screen.
        /// No wallet deduction occurs.
        /// </summary>
        [HttpPatch("{orderId:guid}/cancel")]
        public IActionResult CancelOrder(Guid orderId)
        {
            try
            {
                var success = _orders.CancelOrder(orderId);
                if (!success)
                    return StatusCode(500, new { message = "Failed to cancel order." });

                return Ok(new { message = "Order cancelled successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
