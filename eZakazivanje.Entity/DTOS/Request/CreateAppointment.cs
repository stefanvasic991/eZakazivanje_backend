using System;

namespace eZakazivanje.Entity.DTOS.Request;

public class CreateAppointment
{
    public DateTime? AppointmentDate { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public decimal? TotalPrice { get; set; }
    public bool IsCancelled { get; set; } = false;
    public string? CancellationReason { get; set; }
}
