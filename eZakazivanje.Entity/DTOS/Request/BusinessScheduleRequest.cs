public class BusinessScheduleRequest
{
    public DateTime Date { get; set; }
    public bool IsWorkingDay { get; set; }
    public TimeSpan? CustomStartTime { get; set; }
    public TimeSpan? CustomEndTime { get; set; }
    public string? Note { get; set; }
}

public class BusinessScheduleRangeRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<DayOfWeek> NonWorkingDays { get; set; } = new();
    public TimeSpan? DefaultStartTime { get; set; }
    public TimeSpan? DefaultEndTime { get; set; }
}

public class DeleteBusinessScheduleRangeRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class WorkingDayTemplateDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
}

public class SetBusinessWeeklyTemplateRangeRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<WorkingDayTemplateDto> WorkingDays { get; set; } = new();
} 