public class FreeTimeSlotResponse
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid BusinessId { get; set; }
    public string? Note { get; set; }
    public string? ServiceName { get; set; }
} 