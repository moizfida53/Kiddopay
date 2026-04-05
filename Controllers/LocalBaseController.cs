using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kiddopay.Controllers
{
    [Authorize]
    [Route("[controller]/[action]")]
    [ApiController]
    public class LocalBaseController : ControllerBase
    {
    }
}
