namespace WebUntisApi.Models
{
    public class DayModel
    {
        public SchoolDayEnum SchoolDay { get; set; }
        public List<SubjectModel>? Subjects { get; set; }
    }
}
