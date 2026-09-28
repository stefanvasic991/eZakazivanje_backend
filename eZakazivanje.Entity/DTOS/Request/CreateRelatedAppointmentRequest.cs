public class CreateRelatedAppointmentRequest
{
    public Guid ExistingAppointmentId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid ServiceId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
} 