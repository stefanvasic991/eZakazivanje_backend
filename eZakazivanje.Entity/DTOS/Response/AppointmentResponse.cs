public class AppointmentResponse
{
    public Guid Id { get; set; }
    public string? BusinessName { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public decimal? TotalPrice { get; set; }
    public List<ServiceDto>? Services { get; set; }
}

public class ServiceDto
{
    public string? Name { get; set; }
    public decimal? Price { get; set; }
    // ISO-4217 currency code, e.g. "RSD", "EUR", "USD"
    public string? Currency { get; set; }
    public TimeSpan? Duration { get; set; }
} 