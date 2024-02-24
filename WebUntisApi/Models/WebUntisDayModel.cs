using WebUntisApi.Models.Enums;

namespace WebUntisApi.Models
{
    public class WebUntisDayModel
    {
        public WebUntisSchoolDayEnum WebUntisSchoolDay { get; init; }
        public List<WebUntisRenderEntryModel>? Subjects { get; set; }
    }
}
