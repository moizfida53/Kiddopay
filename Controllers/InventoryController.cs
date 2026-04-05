using Kiddopay.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Kiddopay.Controllers
{
    public class InventoryController(IinventoryService InventoryService) : LocalBaseController
    {
        [HttpGet]
        public IActionResult GetAll()
        {
            var result = InventoryService.GetAll();

            return Ok(result);
        }
    }
}
