namespace WebUntisApi.Models
{
    public class ScheduleEntryModel
    {
        public string? SubjectName { get; set; }
        public string? RoomNumber { get; set; }
        public string? DayOfWeek { get; set; }
        public string? Status { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}
