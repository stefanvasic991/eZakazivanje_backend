using System;


namespace eZakazivanje.Entity.DbSet;

public class Appointment : BaseEntity
{
    public Guid? RelatedAppointmentId { get; set; }
    public Appointment? RelatedAppointment { get; set; }
    public List<Appointment>? RelatedAppointments { get; set; } = new List<Appointment>();

    public DateTime? AppointmentDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public decimal? TotalPrice { get; set; }
    public bool? IsCancelled { get; set; } = false;
    public string? CancellationReason { get; set; }
    public bool? Reminder24HoursSent { get; set; }
    public bool? Reminder2HoursSent { get; set; }

    // Navigation properties

    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public Guid? BusinessId { get; set; }
    public Bussiness? Business { get; set; }

    public List<Service>? Services { get; set; } = new List<Service>();
    public bool IsRelatedAppointment { get; set; } = false;

    public string? ApplicationUserId { get; set; }
    public virtual ApplicationUser? ApplicationUser { get; set; }
}
