using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;    

namespace eZakazivanje.DataService.Repositories;

public class EmployeeRepository : GenericRepository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(AppDbContext context, ILogger logger) : base(context, logger){}

    public async Task<IEnumerable<Employee>> GetAllEmployeesAsync()
    {
        try
        {
            return await _dbSet.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Repo} GetAllEmployees function error", typeof(EmployeeRepository));
            throw;
        }
    }

    public async Task<Employee?> GetEmployeeByIdAsync(Guid id)
    {
        try
        {
            return await _dbSet.FirstOrDefaultAsync(e => e.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Repo} GetEmployeeById function error", typeof(EmployeeRepository));
            throw;
        }
    }

    public async Task<Employee?> CreateEmployeeAsync(Employee employee)
    {
        try
        {
            await _dbSet.AddAsync(employee);
            await _context.SaveChangesAsync();
            return employee;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Repo} CreateEmployee function error", typeof(EmployeeRepository));
            return null;
        }
    }

    public async Task<bool> UpdateEmployeeAsync(Guid id, Employee employee)
    {
        try
        {
            var existingEmployee = await _dbSet.FirstOrDefaultAsync(e => e.Id == id);
            if (existingEmployee == null)
                return false;

            if (employee.FirstName != null)
                existingEmployee.FirstName = employee.FirstName;
            if (employee.LastName != null)
                existingEmployee.LastName = employee.LastName;
            if (employee.ImageUrl != null)
                existingEmployee.ImageUrl = employee.ImageUrl;
            if (employee.IsAvailable != null)
                existingEmployee.IsAvailable = employee.IsAvailable;

            _context.Update(existingEmployee);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Repo} UpdateEmployee function error", typeof(EmployeeRepository));
            return false;
        }
    }

    public async Task<bool> DeleteEmployeeAsync(Guid id)
    {
        try
        {
            var employee = await _dbSet.FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null)
                return false;

            // First, update any appointments that reference this employee
            var appointments = await _context.Appointments
                .Where(a => a.EmployeeId == id)
                .ToListAsync();

            foreach (var appointment in appointments)
            {
                appointment.EmployeeId = null;
            }

            // Now we can safely delete the employee
            _dbSet.Remove(employee);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Repo} DeleteEmployee function error", typeof(EmployeeRepository));
            return false;
        }
    }

    public async Task<bool> UpdateImageAsync(Guid employeeId, string imagePath)
    {
        try
        {
            var employee = await _dbSet.FindAsync(employeeId);
            if (employee == null) return false;

            employee.ImageUrl = imagePath;
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom ažuriranja slike zaposlenog");
            return false;
        }
    }

}
