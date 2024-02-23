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
        public async Task<List<ScheduleEntryModel>> GetClassSchedule([FromQuery] int classId)
        {
            var result = await _client.RetrieveClassData(classId);
            return result;
        }

        [HttpGet]
        public async Task<List<ScheduleEntryModel>> GetClassSubjectsByDate([FromQuery] int classId, [FromQuery] DateTime date)
        {
            var weekSchedule = await _client.RetrieveClassData(classId);

            var dateSchedule = weekSchedule.Where(x => x.StartTime != null && x.StartTime.Value.Date == date.Date).ToList();

            return dateSchedule;
        }
    }
}
