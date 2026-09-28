using System;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Responce;
using eZakazivanje.Entity.DTOS.Response;
using static eZakazivanje.DataService.Repositories.BussinessRepository;

namespace eZakazivanje.DataService.Repositories.Interfaces;

public interface IBussinessRepository : IGenericRepository<Bussiness>
{
    public Task<bool> CreateBusiness(Bussiness business);
    public Task<bool> DeleteBusiness(Guid bussinessId);
    public Task<IEnumerable<Bussiness>> GetAllBusinesses();
    public Task<IEnumerable<Bussiness>> GetAllBusinessesPaginated(int pageNumber, int pageSize);
    public Task<int> GetTotalBusinessCount();
    public Task<BusinessAppointmentsForDate> GetBusinessAppointmentsForDate(Guid businessId, DateTime date);
    
    public Task<bool> UpdateBusinessServices(Guid businessId, BusinessUpdateDto updatedBusiness);

    public Task<IEnumerable<BusinessByCategoryDto>> GetBusinessesByCategory(Guid categoryId);

    Task<BusinessDetailsResponse?> GetBusinessDetails(Guid businessId);

    Task<bool> AddEmployeeToBusiness(Guid businessId, Employee employee);

    Task<IEnumerable<EmployeeResponse>> GetBusinessEmployees(Guid businessId);

    Task<AvailableTimeSlotsResponse> GetAvailableTimeSlots(Guid businessId, Guid employeeId, Guid serviceId, DateTime date);

    Task<bool> UpdateBusinessHours(Guid businessId, TimeSpan startWorkHours, TimeSpan endWorkHours,TimeSpan? tick);

    Task<bool> ToggleBusinessActiveStatusAsync(Guid businessId, bool isActive);
    Task<CreateBussinessResponce?> UpdateBusinessImagesAsync(Guid businessId, List<string> imagePaths);
    Task<bool> AddFreeTimeSlotAsync(Guid businessId, Guid employeeId, DateTime date, TimeSpan startTime, TimeSpan endTime, string? note = null, string? serviceName = null);
    Task<bool> AddFreeTimeSlotRangeAsync(Guid businessId, Guid employeeId, DateTime startDate, DateTime endDate, TimeSpan startTime, TimeSpan endTime, string? note = null, string? serviceName = null);
    Task<bool> DeleteFreeTimeSlotAsync(Guid businessId, Guid freeTimeSlotId);
    Task<bool> DeleteFreeTimeSlotRangeAsync(Guid businessId, Guid employeeId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<FreeTimeSlotResponse>> GetFreeTimeSlotsForDate(Guid businessId, Guid employeeId, DateTime date);
    Task<IEnumerable<FreeTimeSlotResponse>> GetFreeTimeSlotsForDateRange(Guid businessId, Guid employeeId, DateTime startDate, DateTime endDate);
    Task<bool> DeleteBusinessImageAsync(Guid businessId, string imagePath);
    Task<bool> AddBusinessAddress(Guid businessId, Address address,string? PIB,string? phoneNumber);
    Task<bool> UpdateBusinessContactInfo(Guid businessId, string? pib, string? phoneNumber);
    Task<IEnumerable<NearbyBusinessResponse>> GetNearbyBusinesses(double latitude, double longitude, int limit = 5);
    Task<IEnumerable<CategoryWithServices>> GetBusinessCategoriesWithServices(Guid businessId);
    Task<bool> SetBusinessSchedule(Guid businessId, BusinessScheduleRequest request);
    Task<bool> SetBusinessScheduleRange(Guid businessId, BusinessScheduleRangeRequest request);
    Task<IEnumerable<BusinessSchedule>> GetBusinessSchedule(Guid businessId, DateTime startDate, DateTime endDate);
    Task<bool> IsBusinessAvailableOnDate(Guid businessId, DateTime date);
    Task<bool> DeleteBusinessScheduleRange(Guid businessId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<BusinessByCategoryDto>> GetBusinessesByCategoryAndCity(Guid categoryId, string city);
    Task<bool> RemoveBusinessCategories(Guid businessId, IEnumerable<Guid> categoryIdsToKeep);
    Task<bool> SetBusinessWeeklyTemplateRange(Guid businessId, SetBusinessWeeklyTemplateRangeRequest request);
}
