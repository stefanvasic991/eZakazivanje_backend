using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;

namespace eZakazivanje.DataService.Repositories;

public class AddressRepository : GenericRepository<Address>, IAddressRepository
{
    public AddressRepository(AppDbContext context, ILogger logger) : base(context, logger){}

    public async Task<IEnumerable<Address>> GetAllAddresses()
    {
        try
        {
            var addresses = await _dbSet.ToListAsync();
            _logger.LogInformation("Pronadjeno {Count} adresa", addresses.Count);
            return addresses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja svih adresa");
            return Enumerable.Empty<Address>();
        }
    }

    public async Task<Address?> GetAddressById(Guid addressId)
    {
        try
        {
            var address = await _dbSet.FindAsync(addressId);

            if (address == null)
            {
                _logger.LogWarning("Adresa sa ID {AddressId} nije pronađena", addressId);
                return null;
            }

            _logger.LogInformation("Pronadjena adresa {AddressId}", addressId);
            return address;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja adrese {AddressId}", addressId);
            return null;
        }
    }

    public async Task<bool> DeleteAddress(Guid addressId)
    {
        try
        {
            var address = await _dbSet.FindAsync(addressId);

            if (address == null)
            {
                _logger.LogWarning("Adresa sa ID {AddressId} nije pronađena", addressId);
                return false;
            }

            _dbSet.Remove(address);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Adresa {AddressId} je uspešno obrisana", addressId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom brisanja adrese {AddressId}", addressId);
            return false;
        }
    }

    public async Task<Address?> AddAddress(Address address)
    {
        try
        {
            address.CreatedAt = DateTime.UtcNow;
            address.UpdatedAt = DateTime.UtcNow;

            await _dbSet.AddAsync(address);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Adresa je uspešno kreirana sa ID {AddressId}", address.Id);
            return address;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom kreiranja nove adrese");
            return null;
        }
    }

    public async Task<Address?> UpdateAddress(Guid addressId, Address updatedAddress)
    {
        try
        {
            var existingAddress = await _dbSet.FindAsync(addressId);

            if (existingAddress == null)
            {
                _logger.LogWarning("Adresa sa ID {AddressId} nije pronađena", addressId);
                return null;
            }

            // Update only non-null properties
            if (updatedAddress.StreetName != null)
                existingAddress.StreetName = updatedAddress.StreetName;
            if (updatedAddress.Floor != null)
                existingAddress.Floor = updatedAddress.Floor;
            if (updatedAddress.ZipCode != null)
                existingAddress.ZipCode = updatedAddress.ZipCode;
            if (updatedAddress.City != null)
                existingAddress.City = updatedAddress.City;
            if (updatedAddress.State != null)
                existingAddress.State = updatedAddress.State;
                
                existingAddress.Longitude = updatedAddress.Longitude;
                existingAddress.Latitude = updatedAddress.Latitude;
            
            existingAddress.UpdatedAt = DateTime.UtcNow;

            _dbSet.Update(existingAddress);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Adresa {AddressId} je uspešno ažurirana", addressId);
            return existingAddress;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom ažuriranja adrese {AddressId}", addressId);
            return null;
        }
    }
}
