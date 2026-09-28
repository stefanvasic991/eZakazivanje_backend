using System;
using eZakazivanje.Entity.DbSet;
namespace eZakazivanje.DataService.Repositories.Interfaces;

public interface IServiceRepository : IGenericRepository<Service>
{
    Task<IEnumerable<Service>> GetAllServices();
    Task<Service?> GetServiceById(Guid serviceId);
    Task<Service?> CreateService(Service service);
    Task<bool> UpdateService(Guid id, Service service);
    Task<bool> DeleteService(Guid id);
    Task<bool> AddServiceToCategory(Guid serviceId, Guid categoryId);
    Task<bool> AddServiceToBusiness(Guid serviceId, Guid businessId);
    Task<bool> UpdateImageAsync(Guid serviceId, string imagePath);
}
