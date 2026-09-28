using eZakazivanje.Entity.DbSet;

public class FreeTimeSlot
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? Note { get; set; }
    public string? ServiceName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public virtual Bussiness? Business { get; set; }
    public virtual Employee? Employee { get; set; }
} 