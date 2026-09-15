using Kiddopay.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kiddopay.Controllers
{
    /// <summary>
    /// Routes follow LocalBaseController convention: [controller]/[action]
    ///   GET /PreOrders/GetActivePreOrder?studentId={guid}
    ///   GET /PreOrders/GetPreOrderById/{preOrderId}
    /// </summary>
    public class PreOrdersController(IPreOrderService preOrderService) : LocalBaseController
    {
        /// <summary>
        /// Returns the active pre-order for a student on today's date.
        /// Called by Angular after the NFC scan when HasActivePreOrder = true.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetActivePreOrder([FromQuery] Guid studentId)
        {
            try
            {
                if (studentId == Guid.Empty)
                    return BadRequest(new { message = "StudentId is required." });

                var preOrder = await preOrderService.GetActivePreOrderForStudent(studentId);
                if (preOrder == null)
                    return NotFound(new { message = "No active pre-order found for this student today." });

                return Ok(preOrder);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Returns a pre-order by its GUID. Used to refresh state mid-fulfillment.
        /// </summary>
        [HttpGet("{preOrderId:guid}")]
        public async Task<IActionResult> GetPreOrderById(Guid preOrderId)
        {
            try
            {
                if (preOrderId == Guid.Empty)
                    return BadRequest(new { message = "PreOrderId is required." });

                var preOrder = await preOrderService.GetPreOrderById(preOrderId);
                if (preOrder == null)
                    return NotFound(new { message = "Pre-order not found." });

                return Ok(preOrder);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}