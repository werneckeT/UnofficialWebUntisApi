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

        /// Initializes a new instance of the <see cref="WebUntisController"/> class. This constructor sets up the necessary dependencies for the controller, including the WebUntis HTML client and an instance of ILogger for logging purposes.
        public WebUntisController(WebUntisHtmlClient webUntisHtmlClient, ILogger<WebUntisController> logger)
        {
            _webUntisHtmlClient = webUntisHtmlClient;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the current week schedule for a specific class.
        /// </summary>
        /// <param name="cookieKey">The request cookie key.</param>
        /// <param name="classId">The ID of the class.</param>
        /// <returns>A WebUntisWeekModel representing the current week schedule.</returns>
        [HttpGet]
        public async Task<WebUntisWeekModel> GetCurrentWeekSchedule([FromQuery] string cookieKey, [FromQuery] int classId)
        {
            _logger.LogInformation("Request schedule for class {classId} with request-cookie-key: {cookieKey}", classId, cookieKey);
            var result = await _webUntisHtmlClient.RetrieveClassDataAsync(cookieKey, classId);
            return result;
        }

        /// <summary>
        /// Retrieves the week schedule for a specified class ID and offset.
        /// </summary>
        /// <param name="cookieKey">The request cookie key.</param>
        /// <param name="classId">The identifier of the class.</param>
        /// <param name="offset">The offset to retrieve the schedule from.</param>
        /// <returns>A WebUntisWeekModel containing the retrieved week schedule.</returns>
        [HttpGet]
        public async Task<WebUntisWeekModel> GetWeekScheduleByOffset([FromQuery] string cookieKey, [FromQuery] int classId, [FromQuery] int offset)
        {
            _logger.LogInformation("Request schedule (offset: {offset}) for class {classId} with request-cookie-key: {cookieKey}", offset, classId, cookieKey);
            var result = await _webUntisHtmlClient.RetrieveClassDataForWeekAsync(cookieKey, classId, offset);
            return result;
        }
    }
}
