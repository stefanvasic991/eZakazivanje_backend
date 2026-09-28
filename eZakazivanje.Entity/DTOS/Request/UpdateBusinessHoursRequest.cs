public class UpdateBusinessHoursRequest
{
    public TimeSpan StartWorkHours { get; set; }
    public TimeSpan EndWorkHours { get; set; }
    public TimeSpan? Tick { get; set; }
} 