public class CreateAppointmentRequest
{
    public Guid EmployeeId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid ServiceId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
} 