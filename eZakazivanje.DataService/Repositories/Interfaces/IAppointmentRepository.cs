using System;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Request;
using eZakazivanje.Entity.DTOS.Response;
namespace eZakazivanje.DataService.Repositories.Interfaces;

public interface IAppointmentRepository : IGenericRepository<Appointment>
{
    Task<IEnumerable<Appointment>> GetAllAppointments();
    Task<Appointment?> GetAppointmentById(Guid appointmentId);
    //Task<IEnumerable<Appointment>> GetAppointmentsByUserId(Guid userId);
    //Task<IEnumerable<Appointment>> GetAppointmentsByEmployeeId(Guid employeeId);
    //Task<IEnumerable<Appointment>> GetAppointmentsByBussinessId(Guid bussinessId);
    //Task<IEnumerable<Appointment>> GetAppointmentsByServiceId(Guid serviceId);
    //Task<IEnumerable<Appointment>> GetAppointmentsByAddressId(Guid addressId);
    //Task<IEnumerable<Appointment>> GetAppointmentsByDate(DateTime date);
    //Task<IEnumerable<Appointment>> GetAppointmentsByTime(DateTime time);
    Task<Appointment?> CreateAppointment(Appointment appointment);
    Task<bool> UpdateAppointment(Guid appointmentId, Appointment appointment);
    Task<bool> DeleteAppointment(Guid appointmentId);
    Task<(bool Success, string? UserId, Appointment? Appointment)> DeleteAppointmentByBusiness(Guid appointmentId, Guid businessId);
    Task<AppointmentBookingResponse?> CreateUserAppointment(Guid userId, CreateAppointmentRequest request);
    Task<List<AppointmentWithServicesDto>> GetUpcomingAppointmentsForUserAsync(string userId);
    Task<List<AppointmentWithServicesDto>> GetPastAppointmentsForUserAsync(string userId);
    Task<AppointmentBookingResponse?> CreateRelatedAppointment(Guid userId, CreateRelatedAppointmentRequest request);
    Task<AppointmentWithRelatedResponse?> GetAppointmentWithRelated(Guid appointmentId);
}
