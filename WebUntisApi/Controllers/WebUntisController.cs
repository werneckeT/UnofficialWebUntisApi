using Microsoft.AspNetCore.Mvc;
using WebUntisApi.Clients;
using WebUntisApi.Models;

namespace WebUntisApi.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class WebUntisController : ControllerBase
    {
        private readonly WebUntisHtmlClient _webUntisHtmlClient;
        private readonly ILogger<WebUntisController> _logger;

        public WebUntisController(WebUntisHtmlClient webUntisHtmlClient, ILogger<WebUntisController> logger)
        {
            _webUntisHtmlClient = webUntisHtmlClient;
            _logger = logger;
        }

        [HttpGet]
        public async Task<WebUntisWeekModel> GetCurrentWeekSchedule([FromQuery] string cookieKey, [FromQuery] int classId)
        {
            _logger.LogInformation("Request schedule for class {classId} with request-cookie-key: {cookieKey}", classId, cookieKey);
            var result = await _webUntisHtmlClient.RetrieveClassDataAsync(cookieKey, classId);
            return result;
        }

        [HttpGet]
        public async Task<WebUntisWeekModel> GetWeekScheduleByOffset([FromQuery] string cookieKey, [FromQuery] int classId, [FromQuery] int offset)
        {
            _logger.LogInformation("Request schedule (offset: {offset}) for class {classId} with request-cookie-key: {cookieKey}", offset, classId, cookieKey);
            var result = await _webUntisHtmlClient.RetrieveClassDataForWeekAsync(cookieKey, classId, offset);
            return result;
        }
    }
}
