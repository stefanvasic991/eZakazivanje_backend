public class AppointmentBookingResponse
{
    public Guid AppointmentId { get; set; }
    public Guid? RelatedAppointmentId { get; set; }
    public ServiceBookingInfo? Service { get; set; }
    public EmployeeBookingInfo? Employee { get; set; }
    public decimal Total { get; set; }
}

public class ServiceBookingInfo
{
    public Guid ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public decimal ServicePrice { get; set; }
    public TimeSpan ServiceDuration { get; set; }
}

public class EmployeeBookingInfo
{
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
} 