using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;    
using eZakazivanje.Entity.DTOS.Response;
using eZakazivanje.Entity.DTOS.Request;

namespace eZakazivanje.DataService.Repositories;

public class AppointmentRepository : GenericRepository<Appointment>, IAppointmentRepository
{
    public AppointmentRepository(AppDbContext context, ILogger logger) : base(context, logger){}

    public async Task<IEnumerable<Appointment>> GetAllAppointments()
    {
        try
        {
            var appointments = await _dbSet.ToListAsync();
            _logger.LogInformation("Pronadjeno {Count} termina", appointments.Count);
            return appointments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja svih termina");
            return [];
        }
    }

    public async Task<Appointment?> GetAppointmentById(Guid appointmentId)
    {
        try
        {
            var appointment = await _dbSet.FindAsync(appointmentId);

            if (appointment == null)
            {
                _logger.LogWarning("Termin sa ID {AppointmentId} nije pronađen", appointmentId);
                return null;
            }

            _logger.LogInformation("Pronadjen termin {AppointmentId}", appointmentId);
            return appointment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja termina {AppointmentId}", appointmentId);
            return null;
        }
    }

    public async Task<Appointment?> CreateAppointment(Appointment appointment)
    {
        try
        {
            var result = await _dbSet.AddAsync(appointment);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Kreiran novi termin sa ID {AppointmentId}", appointment.Id);
            return result.Entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom kreiranja termina");
            return null;
        }
    }

    public async Task<bool> UpdateAppointment(Guid appointmentId, Appointment appointment)
    {
        try
        {
            
            var existingAppointment = await _dbSet.FindAsync(appointmentId);

            
            if (existingAppointment == null)
            {
                _logger.LogWarning("Termin sa ID {AppointmentId} nije pronađen za ažuriranje", appointment.Id);
                return false;
            }

            existingAppointment.StartTime = appointment.StartTime ?? existingAppointment.StartTime;
            existingAppointment.EndTime = appointment.EndTime ?? existingAppointment.EndTime;
            existingAppointment.AppointmentDate = appointment.AppointmentDate ?? existingAppointment.AppointmentDate;
            existingAppointment.EmployeeId = appointment.EmployeeId ?? existingAppointment.EmployeeId;
            existingAppointment.ApplicationUserId = appointment.ApplicationUserId ?? existingAppointment.ApplicationUserId;
            existingAppointment.BusinessId = appointment.BusinessId ?? existingAppointment.BusinessId;
            existingAppointment.TotalPrice = appointment.TotalPrice ?? existingAppointment.TotalPrice;
            existingAppointment.IsCancelled = appointment.IsCancelled ?? existingAppointment.IsCancelled;
            existingAppointment.CancellationReason = appointment.CancellationReason ?? existingAppointment.CancellationReason;
            existingAppointment.UpdatedAt = DateTime.UtcNow;
        

            _dbSet.Update(existingAppointment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ažuriran termin {AppointmentId}", appointment.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom ažuriranja termina {AppointmentId}", appointment.Id);
            return false;
        }
    }

    public async Task<bool> DeleteAppointment(Guid appointmentId)
    {
        try
        {
            // First, find all related appointments
            var relatedAppointments = await _context.Appointments
                .Where(a => a.RelatedAppointmentId == appointmentId)
                .ToListAsync();

            // Detach all related appointments by setting RelatedAppointmentId to null
            foreach (var relatedAppointment in relatedAppointments)
            {
                relatedAppointment.RelatedAppointmentId = null;
                relatedAppointment.IsRelatedAppointment = false;
            }

            // Then find and delete the main appointment
            var appointment = await _context.Appointments.FindAsync(appointmentId);
            if (appointment == null)
            {
                return false;
            }

            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom brisanja termina {AppointmentId}", appointmentId);
            return false;
        }
    }

    public async Task<(bool Success, string? UserId, Appointment? Appointment)> DeleteAppointmentByBusiness(Guid appointmentId, Guid businessId)
    {
        try
        {
            // Get the appointment with all necessary details
            var appointment = await _context.Appointments
                .Include(a => a.Business)
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
            {
                _logger.LogWarning("Termin nije pronađen");
                return (false, null, null);
            }

            // Verify that the business owns this appointment
            if (appointment.BusinessId != businessId)
            {
                _logger.LogWarning("Biznis nema dozvolu da briše termin");
                return (false, null, null);
            }

            // Store user ID for notification before deletion
            var userId = appointment.ApplicationUserId;
            var appointmentCopy = new Appointment
            {
                AppointmentDate = appointment.AppointmentDate,
                StartTime = appointment.StartTime,
                EndTime = appointment.EndTime,
                Business = appointment.Business,
                Employee = appointment.Employee
            };

            // Delete only this specific appointment
            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Termin je uspešno obrisan od strane biznisa");
            return (true, userId, appointmentCopy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom brisanja termina od strane biznisa");
            return (false, null, null);
        }
    }

    public async Task<AppointmentBookingResponse?> CreateUserAppointment(Guid userId, CreateAppointmentRequest request)
    {
        try
        {
            // Validate all required entities exist
            var userIdString = userId.ToString();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userIdString);
            var business = await _context.Businesses
                .FindAsync(request.BusinessId);
            var employee = await _context.Employees
                .FindAsync(request.EmployeeId);
            var service = await _context.Services
                .FindAsync(request.ServiceId);

            if (user == null || business == null || employee == null || 
                service == null || service.Duration == null)
            {
                _logger.LogWarning("Jedna ili više obaveznih entiteta nije pronađena.");
                return null;
            }

            // Check if the time slot is available
            var endTime = request.StartTime.Add(service.Duration.Value);
            var isSlotAvailable = await IsTimeSlotAvailableAsync(request.EmployeeId, request.AppointmentDate, request.StartTime, endTime);
            
            if (!isSlotAvailable)
            {
                _logger.LogWarning("Traženi termin nije dostupan za zaposlenog {EmployeeId} na datum {Date} u vremenu {StartTime}-{EndTime}", 
                    request.EmployeeId, request.AppointmentDate, request.StartTime, endTime);
                return null;
            }

            // Create appointment
            var appointment = new Appointment
            {
                Id = Guid.NewGuid(),
                ApplicationUserId = userId.ToString(),
                BusinessId = request.BusinessId,
                EmployeeId = request.EmployeeId,
                AppointmentDate = request.AppointmentDate,
                StartTime = request.StartTime,
                EndTime = endTime,
                TotalPrice = service.Price,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsCancelled = false,
                IsRelatedAppointment = false
            };

            // Add and save the appointment
            var result = await _dbSet.AddAsync(appointment);
            await _context.SaveChangesAsync();

            // Create the many-to-many relationship with service using navigation properties
            appointment.Services = new List<Service> { service };
            await _context.SaveChangesAsync();

            // Calculate total price including related appointments
            decimal totalPrice = service.Price ?? 0;
            if (appointment.RelatedAppointmentId.HasValue)
            {
                var relatedAppointment = await _dbSet
                    .Include(a => a.Services)
                    .FirstOrDefaultAsync(a => a.Id == appointment.RelatedAppointmentId);
                
                if (relatedAppointment?.Services != null)
                {
                    totalPrice += relatedAppointment.Services.Sum(s => s.Price ?? 0);
                }
            }

            // Create response
            return new AppointmentBookingResponse
            {
                AppointmentId = appointment.Id,
                RelatedAppointmentId = appointment.RelatedAppointmentId,
                Service = new ServiceBookingInfo
                {
                    ServiceId = service.Id,
                    ServiceName = service.Name ?? "",
                    ServicePrice = service.Price ?? 0,
                    ServiceDuration = service.Duration ?? TimeSpan.Zero
                },
                Employee = new EmployeeBookingInfo
                {
                    EmployeeId = employee.Id,
                    EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim()
                },
                Total = totalPrice
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom kreiranja termina za korisnika {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Checks if a time slot is available for booking
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <param name="appointmentDate">Appointment date</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <returns>True if slot is available, false otherwise</returns>
    private async Task<bool> IsTimeSlotAvailableAsync(Guid employeeId, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime)
    {
        try
        {
            // Check for overlapping appointments for the same employee on the same date
            var conflictingAppointments = await _context.Appointments
                .Where(a => a.EmployeeId == employeeId &&
                           a.AppointmentDate == appointmentDate &&
                           a.IsCancelled != true && // Exclude cancelled appointments
                           ((a.StartTime <= startTime && a.EndTime > startTime) || // New appointment starts during existing
                            (a.StartTime < endTime && a.EndTime >= endTime) || // New appointment ends during existing
                            (a.StartTime >= startTime && a.EndTime <= endTime))) // New appointment completely contains existing
                .AnyAsync();

            return !conflictingAppointments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom provere dostupnosti termina za zaposlenog {EmployeeId}", employeeId);
            return false; // Fail safe - assume slot is not available if there's an error
        }
    }

    public async Task<List<AppointmentWithServicesDto>> GetUpcomingAppointmentsForUserAsync(string userId)
    {
        try
        {
            var currentDateTime = DateTime.UtcNow;

            var appointments = await _context.Appointments
                .Include(a => a.Business)
                .Include(a => a.Employee)
                .Include(a => a.Services)
                .Where(a => a.ApplicationUserId == userId)
                .Where(a => a.AppointmentDate != null && a.AppointmentDate.Value.Date >= currentDateTime.Date)
                .ToListAsync();

            // Filter and transform the appointments
            var filteredAppointments = appointments
                .Where(a => 
                    a.AppointmentDate != null && 
                    (a.AppointmentDate.Value.Date > currentDateTime.Date || 
                    (a.AppointmentDate.Value.Date == currentDateTime.Date && 
                     a.StartTime > currentDateTime.TimeOfDay)))
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .Select(a => new AppointmentWithServicesDto
                {
                    Appointment = a,
                    ServiceIds = a.Services?.Select(s => s.Id).ToList() ?? new List<Guid>()
                })
                .ToList();

            _logger.LogInformation("Pronadjeno {Count} budućih termina sa uslugama za korisnika {UserId}", 
                filteredAppointments.Count, userId);

            return filteredAppointments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja budućih termina za korisnika {UserId}", userId);
            return new List<AppointmentWithServicesDto>();
        }
    }

    public async Task<List<AppointmentWithServicesDto>> GetPastAppointmentsForUserAsync(string userId)
    {
        try
        {
            var currentDateTime = DateTime.UtcNow;

            var appointments = await _context.Appointments
                .Include(a => a.Business)
                .Include(a => a.Employee)
                .Include(a => a.Services)
                .Where(a => a.ApplicationUserId == userId)
                .Where(a => a.AppointmentDate != null && a.AppointmentDate.Value.Date <= currentDateTime.Date)
                .ToListAsync();

            // Filter and transform the appointments
            var filteredAppointments = appointments
                .Where(a => 
                    a.AppointmentDate != null && 
                    (a.AppointmentDate.Value.Date < currentDateTime.Date || 
                    (a.AppointmentDate.Value.Date == currentDateTime.Date && 
                     a.StartTime < currentDateTime.TimeOfDay)))
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .Select(a => new AppointmentWithServicesDto
                {
                    Appointment = a,
                    ServiceIds = a.Services?.Select(s => s.Id).ToList() ?? new List<Guid>()
                })
                .ToList();

            _logger.LogInformation("Pronadjeno {Count} završenih termina sa uslugama za korisnika {UserId}", 
                filteredAppointments.Count, userId);

            return filteredAppointments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja završenih termina za korisnika {UserId}", userId);
            return new List<AppointmentWithServicesDto>();
        }
    }

    public async Task<AppointmentBookingResponse?> CreateRelatedAppointment(Guid userId, CreateRelatedAppointmentRequest request)
    {
        try
        {
            // Get the existing appointment
            var existingAppointment = await _dbSet
                .Include(a => a.Business)
                .FirstOrDefaultAsync(a => a.Id == request.ExistingAppointmentId);

            if (existingAppointment == null)
            {
                _logger.LogWarning("Traženi postojeći termin nije pronađen");
                return null;
            }

            // Validate entities
            var service = await _context.Services.FindAsync(request.ServiceId);
            var employee = await _context.Employees.FindAsync(request.EmployeeId);

            if (service == null || employee == null || service.Duration == null)
            {
                _logger.LogWarning("Obavezni entiteti nisu pronađeni");
                return null;
            }

            // Check if the time slot is available
            var endTime = request.StartTime.Add(service.Duration.Value);
            var isSlotAvailable = await IsTimeSlotAvailableAsync(request.EmployeeId, request.AppointmentDate, request.StartTime, endTime);
            
            if (!isSlotAvailable)
            {
                _logger.LogWarning("Traženi termin nije dostupan za zaposlenog {EmployeeId} na datum {Date} u vremenu {StartTime}-{EndTime}", 
                    request.EmployeeId, request.AppointmentDate, request.StartTime, endTime);
                return null;
            }

            // Create the new related appointment
            var relatedAppointment = new Appointment
            {
                Id = Guid.NewGuid(),
                ApplicationUserId = userId.ToString(),
                BusinessId = existingAppointment.BusinessId,
                EmployeeId = request.EmployeeId,
                AppointmentDate = request.AppointmentDate,
                StartTime = request.StartTime,
                EndTime = endTime,
                RelatedAppointmentId = existingAppointment.Id,
                IsRelatedAppointment = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsCancelled = false
            };

            // Add and save the appointment first
            var result = await _dbSet.AddAsync(relatedAppointment);
            await _context.SaveChangesAsync();

            // Then create the service relationship using EF Core instead of raw SQL
            relatedAppointment.Services = new List<Service> { service };
            await _context.SaveChangesAsync();

            // Calculate total price including both appointments
            decimal totalPrice = (service.Price ?? 0) + (existingAppointment.TotalPrice ?? 0);
            relatedAppointment.TotalPrice = totalPrice;
            await _context.SaveChangesAsync();

            return new AppointmentBookingResponse
            {
                AppointmentId = relatedAppointment.Id,
                RelatedAppointmentId = existingAppointment.Id,
                Service = new ServiceBookingInfo
                {
                    ServiceId = service.Id,
                    ServiceName = service.Name ?? "",
                    ServicePrice = service.Price ?? 0,
                    ServiceDuration = service.Duration ?? TimeSpan.Zero
                },
                Employee = new EmployeeBookingInfo
                {
                    EmployeeId = employee.Id,
                    EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim()
                },
                Total = totalPrice
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom kreiranja povezanog termina");
            return null;
        }
    }

    public async Task<AppointmentWithRelatedResponse?> GetAppointmentWithRelated(Guid appointmentId)
    {
        try
        {
            var appointment = await _dbSet
                .Include(a => a.Business)
                .Include(a => a.Employee)
                .Include(a => a.Services)
                .Include(a => a.RelatedAppointment!)
                    .ThenInclude(ra => ra.Services!)
                .Include(a => a.RelatedAppointments!)
                    .ThenInclude(ra => ra.Services!)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
            {
                _logger.LogWarning("Termin sa ID {AppointmentId} nije pronađen", appointmentId);
                return null;
            }

            var response = new AppointmentWithRelatedResponse
            {
                MainAppointment = MapToAppointmentResponse(appointment),
                RelatedAppointment = appointment.RelatedAppointment != null 
                    ? MapToAppointmentResponse(appointment.RelatedAppointment) 
                    : null,
                RelatedAppointments = appointment.RelatedAppointments?
                    .Select(MapToAppointmentResponse)
                    .ToList() ?? new List<AppointmentResponse>()
            };

            _logger.LogInformation("Pronadjen termin {AppointmentId} sa povezanim terminima", appointmentId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja termina {AppointmentId} sa povezanim terminima", appointmentId);
            return null;
        }
    }

    private AppointmentResponse MapToAppointmentResponse(Appointment appointment)
    {
        return new AppointmentResponse
        {
            Id = appointment.Id,
            BusinessName = appointment.Business?.Name,
            EmployeeId = appointment.EmployeeId,
            EmployeeName = $"{appointment.Employee?.FirstName} {appointment.Employee?.LastName}",
            AppointmentDate = appointment.AppointmentDate,
            StartTime = appointment.StartTime,
            EndTime = appointment.EndTime,
            TotalPrice = appointment.TotalPrice,
            Services = appointment.Services?.Select(s => new ServiceDto
            {
                Name = s.Name,
                Price = s.Price,
                Duration = s.Duration
            }).ToList()
        };
    }

}
