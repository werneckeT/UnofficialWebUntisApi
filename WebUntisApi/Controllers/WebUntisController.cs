using Microsoft.AspNetCore.Mvc;
using WebUntisApi.Clients;
using WebUntisApi.Models;

namespace WebUntisApi.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class WebUntisController : ControllerBase
    {
        private readonly WebUntisHtmlClient _client;
        public WebUntisController(WebUntisHtmlClient webUntisHtmlClient)
        {
            _client = webUntisHtmlClient;
        }

        [HttpGet]
        public async Task<WebUntisWeekModel> GetCurrentWeekSchedule([FromQuery] string cookieKey, [FromQuery] int classId)
        {
            var result = await _client.RetrieveClassData(cookieKey, classId);
            return result;
        }
    }
}
