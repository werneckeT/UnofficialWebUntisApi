using WebUntisApi.Models.Enums;

namespace WebUntisApi.Models
{
    public class WebUntisRenderEntryModel
    {
        public string? Name { get; set; }
        public string? Room { get; set; }
        public DateTime? StartTime { get; init; }
        public DateTime? EndTime { get; set; }
        public WebUntisRenderEntryStatusEnum RenderEntryStatus { get; set; }
    }
}
