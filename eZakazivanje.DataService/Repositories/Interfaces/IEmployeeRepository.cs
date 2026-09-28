using System;
using eZakazivanje.Entity.DbSet;
namespace eZakazivanje.DataService.Repositories.Interfaces;

public interface IEmployeeRepository : IGenericRepository<Employee>
{
    Task<IEnumerable<Employee>> GetAllEmployeesAsync();
    Task<Employee?> GetEmployeeByIdAsync(Guid id);
    Task<Employee?> CreateEmployeeAsync(Employee employee);
    Task<bool> UpdateEmployeeAsync(Guid id, Employee employee);
    Task<bool> DeleteEmployeeAsync(Guid id);
    Task<bool> UpdateImageAsync(Guid employeeId, string imagePath);
}
