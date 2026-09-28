public class AppointmentWithRelatedResponse
{
    public AppointmentResponse? MainAppointment { get; set; }
    public AppointmentResponse? RelatedAppointment { get; set; }
    public List<AppointmentResponse> RelatedAppointments { get; set; } = new List<AppointmentResponse>();
} 