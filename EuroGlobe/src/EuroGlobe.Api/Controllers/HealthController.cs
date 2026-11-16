using Microsoft.AspNetCore.Mvc;

namespace EuroGlobe.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetStatus() => Ok(new
        {
            Service = "EuroGlobe API",
            Status = "Healthy",
            TimeStamp = DateTime.Now,
        });
    }
}
