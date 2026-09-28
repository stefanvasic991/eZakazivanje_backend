using eZakazivanje.Entity.DbSet;

public class BusinessSchedule
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public DayOfWeek? DayOfWeek{ get; set; }
    public DateTime Date { get; set; }
    public bool IsWorkingDay { get; set; }
    public TimeSpan? CustomStartTime { get; set; }
    public TimeSpan? CustomEndTime { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public virtual Bussiness Business { get; set; } = null!;
} 