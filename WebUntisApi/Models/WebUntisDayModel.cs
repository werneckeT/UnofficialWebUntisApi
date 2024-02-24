using WebUntisApi.Models.Enums;

namespace WebUntisApi.Models
{
    public class WebUntisDayModel
    {
        public WebUntisSchoolDayEnum WebUntisSchoolDay { get; set; }
        public List<WebUntisRenderEntryModel>? Subjects { get; set; }
    }
}
