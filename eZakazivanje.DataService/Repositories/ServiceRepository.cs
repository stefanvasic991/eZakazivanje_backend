using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;    

namespace eZakazivanje.DataService.Repositories;

public class ServiceRepository : GenericRepository<Service>, IServiceRepository
{
    public ServiceRepository(AppDbContext context, ILogger logger) : base(context, logger){}
    public async Task<IEnumerable<Service>> GetAllServices()
    {
        try
        {
            var services = await _dbSet.ToListAsync();
            _logger.LogInformation("Services retrieved successfully");
            return services;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom pronalaženja svih usluga");
            return Enumerable.Empty<Service>();
        }
    }

    public async Task<Service?> GetServiceById(Guid serviceId)
    {
        try
        {
            var service = await _dbSet.FindAsync(serviceId);

            if (service == null)
            {
                _logger.LogWarning("Service not found");
                return null;
            }

            _logger.LogInformation("Service retrieved successfully");
            return service;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving service");
            return null;
        }
    }

    public async Task<Service?> CreateService(Service service)
    {
        try
        {
            var result = await _dbSet.AddAsync(service);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Service created successfully");
            return service;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service");
            return null;
        }
    }

    public async Task<bool> UpdateService(Guid id, Service service)
    {
        try
        {
            var existingService = await _dbSet.FindAsync(id);
            if (existingService == null)
            {
                _logger.LogWarning("Service not found for update");
                return false;
            }

            if (service.Name != null)
                existingService.Name = service.Name;
            if (service.Description != null)
                existingService.Description = service.Description;
            if (service.Price != null)
                existingService.Price = service.Price;
            if (service.Duration != null)
                existingService.Duration = service.Duration;
            if (service.ImageUrl != null)
                existingService.ImageUrl = service.ImageUrl;

            _dbSet.Update(service);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Service updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service");
            return false;
        }
    }

    public async Task<bool> DeleteService(Guid serviceId)
    {
        try
        {
            var service = await _dbSet.FindAsync(serviceId);
            if (service == null)
            {
                _logger.LogWarning("Service not found for deletion");
                return false;
            }

            _dbSet.Remove(service);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Service deleted successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting service");
            return false;
        }
    }

    public async Task<bool> AddServiceToCategory(Guid serviceId, Guid categoryId)
    {
        try
        {
            var service = await _dbSet
                .Include(s => s.Category)
                .FirstOrDefaultAsync(s => s.Id == serviceId);

            if (service == null)
            {
                _logger.LogWarning("Service not found");
                return false;
            }

            var category = await _context.Set<Category>()
                .FindAsync(categoryId);

            if (category == null)
            {
                _logger.LogWarning("Category not found");
                return false;
            }

            service.Category = category;
            service.CategoryId = categoryId;

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Service added to category successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding service to category");
            return false;
        }
    }

    public async Task<bool> AddServiceToBusiness(Guid serviceId, Guid businessId)
    {
        try
        {
            var service = await _dbSet.FindAsync(serviceId);
            if (service == null)
            {
                _logger.LogWarning("Service not found");
                return false;
            }

            var business = await _context.Set<Bussiness>().FindAsync(businessId);
            if (business == null)
            {
                _logger.LogWarning("Business not found");
                return false;
            }

            service.BusinessId = businessId;
            _context.Update(service);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Service added to business successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding service to business");
            return false;
        }
    }

    public async Task<bool> UpdateImageAsync(Guid serviceId, string imagePath)
    {
        try
        {
            var service = await _dbSet.FindAsync(serviceId);
            if (service == null) return false;

            service.ImageUrl = imagePath;
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Service image updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service image");
            return false;
        }
    }

}
