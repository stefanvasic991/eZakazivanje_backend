using System;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Response;

namespace eZakazivanje.DataService.Repositories.Interfaces;

public interface IUserRepository:IGenericRepository<ApplicationUser>
{
    public Task<bool> UpdateImageAsync(Guid userId, string imagePath);
    public Task<bool> DeleteUserAsync(Guid userId);
    public Task<BusinessUserDetailsDto?> GetBusinessUserDetailsByBusinessIdAsync(Guid businessId);
}
