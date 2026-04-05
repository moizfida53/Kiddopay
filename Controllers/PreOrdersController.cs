using Kiddopay.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;

namespace Kiddopay.Controllers
{
    public class PreOrdersController(IPreOrderService preOrderService) : LocalBaseController
    {

        /// <summary>
        /// Returns the active pre-order for a student on today's date.
        /// Angular calls this after the NFC scan when HasActivePreOrder = true.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetActivePreOrder([FromQuery] Guid studentId)
        {
            try
            {
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
        public IActionResult GetPreOrderById(Guid preOrderId)
        {
            try
            {
                var preOrder = preOrderService.GetPreOrderById(preOrderId);
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
