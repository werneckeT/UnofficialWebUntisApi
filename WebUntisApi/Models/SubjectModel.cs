namespace WebUntisApi.Models
{
    public class SubjectModel
    {
        public string? Name { get; set; }
        public string? Room { get; set; }
        //public string? Time { get; set; } //I removed this property
        public DateTime? StartTime { get; set; } // We need to implement this
        public DateTime? EndTime { get; set; } // We need to implement this
        public string? AdditionalInformation { get; set; }
    }
}
