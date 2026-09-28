public class AvailableTimeSlotsResponse
{
    public DateTime Date { get; set; }
    public List<TimeSpan> AvailableTimeSlots { get; set; } = new List<TimeSpan>();
} 