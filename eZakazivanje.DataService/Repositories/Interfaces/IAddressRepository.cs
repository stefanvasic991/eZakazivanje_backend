using System;
using eZakazivanje.Entity.DbSet;

namespace eZakazivanje.DataService.Repositories.Interfaces;

public interface IAddressRepository : IGenericRepository<Address>
{
    Task<IEnumerable<Address>> GetAllAddresses();
    Task<Address?> GetAddressById(Guid addressId);
    Task<Address?> AddAddress(Address address);
    Task<Address?> UpdateAddress(Guid addressId, Address updatedAddress);
    Task<bool> DeleteAddress(Guid addressId);
}
