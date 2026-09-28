using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Responce;
using eZakazivanje.Entity.DTOS.Response;

namespace eZakazivanje.DataService.Repositories;

public class BussinessRepository : GenericRepository<Bussiness>, IBussinessRepository
{
    public BussinessRepository(AppDbContext context, ILogger logger) : base(context, logger){}

    public async Task<bool> CreateBusiness(Bussiness business)
    {
        if (business == null)
        {
            _logger.LogError("Pokušaj kreiranja null biznis entiteta");
            return false;
        }

        try
        {
            // Validate business data
            if (string.IsNullOrWhiteSpace(business.Name))
            {
                _logger.LogError("Naziv biznis entiteta je obavezan");
                return false;
            }

            // Check if a business with the same name already exists
            var existingBusiness = await _dbSet.Where(b => b.Name == business.Name).FirstOrDefaultAsync();
            if (existingBusiness != null)
            {
                _logger.LogWarning($"Biznis entitet sa tim nazivom već postoji");
                return false;
            }

            // Set creation date
            business.CreatedAt = DateTime.UtcNow;
            business.UpdatedAt = DateTime.UtcNow;

            // Add the business using the base class method
            var result = await base.Add(business);

            if (result)
            {
                // GenericRepository.Add only tracks the entity; callers like AuthService.Registration
                // do not call UnitOfWork.CompleteAsync(), so we must persist here.
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Biznis entitet kreiran uspešno");
            }
            else
            {
                _logger.LogError($"Greška prilikom kreiranja biznis entiteta");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Greška prilikom kreiranja biznis entiteta");
            return false;
        }
    }

    public async Task<bool> DeleteBusiness(Guid bussinessId)
    {
        if (bussinessId == Guid.Empty)
        {
            _logger.LogError("Neispravan ID biznis entiteta za brisanje");
            return false;
        }

        try
        {
            // Load the business with all related entities that need to be handled
            var business = await _dbSet
                .Include(b => b.Addresses)
                .Include(b => b.Employees)
                .Include(b => b.Services)
                .Include(b => b.Appointments)
                .FirstOrDefaultAsync(b => b.Id == bussinessId);

            if (business == null)
            {
                _logger.LogWarning($"Biznis entitet nije pronađen za brisanje");
                return false;
            }

            // Remove related entities first
            if (business.Employees != null && business.Employees.Any())
            {
                _context.Employees.RemoveRange(business.Employees);
                _logger.LogInformation($"Uklonjeno {business.Employees.Count} zaposlenih vezanih za biznis");
            }

            if (business.Services != null && business.Services.Any())
            {
                _context.Services.RemoveRange(business.Services);
                _logger.LogInformation($"Uklonjeno {business.Services.Count} usluga vezanih za biznis");
            }

            if (business.Appointments != null && business.Appointments.Any())
            {
                _context.Appointments.RemoveRange(business.Appointments);
                _logger.LogInformation($"Uklonjeno {business.Appointments.Count} termina vezanih za biznis");
            }

            // Now remove the business itself
            _dbSet.Remove(business);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Biznis entitet obrisan uspešno sa svim povezanim entitetima");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Greška prilikom brisanja biznis entiteta");
            return false;
        }
    }

    public async Task<IEnumerable<Bussiness>> GetAllBusinesses()
    {
        try
        {
            var businesses = await _dbSet.ToListAsync();
            return businesses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom pronalaženja svih biznis entiteta");
            return Enumerable.Empty<Bussiness>();
        }
    }

    public async Task<IEnumerable<Bussiness>> GetAllBusinessesPaginated(int pageNumber, int pageSize)
    {
        try
        {
            var businesses = await _dbSet
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            _logger.LogInformation($"Pronadjeno {businesses.Count} biznis entiteta za stranicu {pageNumber} sa veličinom stranice {pageSize}");
            return businesses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Greška prilikom pronalaženja biznis entiteta za stranicu {pageNumber} sa veličinom stranice {pageSize}");
            return Enumerable.Empty<Bussiness>();
        }
    }

    public async Task<int> GetTotalBusinessCount()
    {
        try
        {
            var count = await _dbSet.CountAsync();
            _logger.LogInformation($"Ukupan broj biznis entiteta");
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom pronalaženja ukupnog broja biznis entiteta");
            return 0;
        }
    }


    public async Task<BusinessAppointmentsForDate> GetBusinessAppointmentsForDate(Guid businessId, DateTime date)
    {
        try
        {
            // Ensure the date is in UTC
            var utcDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);

            var businessAppointments = await _dbSet
                .Where(b => b.Id == businessId)
                .Select(b => new BusinessAppointmentsForDate
                {
                    BusinessId = b.Id,
                    Appointments = b.Appointments != null
                        ? b.Appointments
                            .Where(a => a.AppointmentDate.HasValue && a.AppointmentDate.Value.Date == utcDate.Date)
                            .Select(a => new AppointmentInfo
                            {
                                AppointmentId = a.Id,
                                AppointmentDate = a.AppointmentDate,
                                Services = a.Services != null
                                    ? a.Services.Select(s => new ServiceInfo
                                    {
                                        Name = s.Name ?? string.Empty,
                                        ImageUrl = s.ImageUrl ?? string.Empty,
                                        Price = s.Price ?? 0,
                                        Currency = string.IsNullOrWhiteSpace(s.Currency) ? null : s.Currency
                                    }).ToList()
                                    : new List<ServiceInfo>(),
                                StartTime = a.StartTime ?? TimeSpan.Zero,
                                EmployeeId = a.EmployeeId ?? Guid.Empty,
                                EmployeeName = (a.Employee != null ? 
                                    $"{a.Employee.FirstName ?? ""} {a.Employee.LastName ?? ""}".Trim() : "N/A") ?? "N/A",
                                EmployeeImageUrl = a.Employee != null ? a.Employee.ImageUrl ?? string.Empty : string.Empty,
                                AppointmentUserName = a.ApplicationUser != null ? 
                                    $"{a.ApplicationUser.FirstName ?? ""} {a.ApplicationUser.LastName ?? ""}".Trim() : string.Empty,
                                AppointmentUserImageUrl = a.ApplicationUser != null ? a.ApplicationUser.ImageUrl ?? string.Empty : string.Empty,
                                UserPhoneNumber = a.ApplicationUser != null ? a.ApplicationUser.PhoneNumber ?? string.Empty : string.Empty
                            })
                            .ToList()
                        : new List<AppointmentInfo>()
                })
                .FirstOrDefaultAsync();

            if (businessAppointments == null)
            {
                _logger.LogInformation("Nema termina pronađeno za biznis entitet na zadati dan");
                return new BusinessAppointmentsForDate { BusinessId = businessId, Appointments = new List<AppointmentInfo>() };
            }

            _logger.LogInformation("Pronadjeno {Count} termina za biznis entitet na zadati dan", 
                businessAppointments.Appointments.Count);
            return businessAppointments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom pronalaženja termina za biznis entitet na zadati dan");
            return new BusinessAppointmentsForDate { BusinessId = businessId, Appointments = new List<AppointmentInfo>() };
        }
    }
       

public class BusinessAppointmentsForDate
{
    public Guid BusinessId { get; set; }
    public List<AppointmentInfo> Appointments { get; set; } = new();
}

public class AppointmentInfo
{
    public Guid AppointmentId { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeImageUrl { get; set; } = string.Empty;
    public string AppointmentUserName { get; set; } = string.Empty;
    public string AppointmentUserImageUrl { get; set; } = string.Empty;
    public string UserPhoneNumber { get; set; } = string.Empty;
    public List<ServiceInfo> Services { get; set; } = new();
}

public class ServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    // ISO-4217 currency code, e.g. "RSD", "EUR", "USD"
    public string? Currency { get; set; }
}




public async Task<bool> UpdateBusinessServices(Guid businessId, BusinessUpdateDto updatedBusiness)
{
    try
    {
        var existingBusiness = await _dbSet
            .Include(b => b.Services)
            .FirstOrDefaultAsync(b => b.Id == businessId);

        if (existingBusiness == null)
        {
            _logger.LogWarning("Biznis entitet  nije pronađen");
            return false;
        }

        // Initialize Services collection if null
        existingBusiness.Services ??= new List<Service>();

        if (updatedBusiness.Services != null)
        {
            foreach (var serviceDto in updatedBusiness.Services)
            {
                if (serviceDto.Id != Guid.Empty)
                {
                    // Update existing service
                    var existingService = existingBusiness.Services.FirstOrDefault(s => s.Id == serviceDto.Id);
                    if (existingService != null)
                    {
                        var normalizedCurrency = string.IsNullOrWhiteSpace(serviceDto.Currency)
                            ? null
                            : serviceDto.Currency.Trim().ToUpperInvariant();

                        if (normalizedCurrency != null && normalizedCurrency.Length != 3)
                        {
                            _logger.LogWarning("Invalid currency code '{Currency}' for service {ServiceId}", normalizedCurrency, serviceDto.Id);
                            return false;
                        }

                        _context.Entry(existingService).CurrentValues.SetValues(new
                        {
                            serviceDto.Name,
                            serviceDto.Description,
                            serviceDto.Price,
                            serviceDto.Duration,
                            Currency = normalizedCurrency ?? existingService.Currency,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    var normalizedCurrency = string.IsNullOrWhiteSpace(serviceDto.Currency)
                        ? string.Empty
                        : serviceDto.Currency.Trim().ToUpperInvariant();

                    if (!string.IsNullOrEmpty(normalizedCurrency) && normalizedCurrency.Length != 3)
                    {
                        _logger.LogWarning("Invalid currency code '{Currency}' for new service", normalizedCurrency);
                        return false;
                    }

                    // Add new service
                    var newService = new Service
                    {
                        Id = Guid.NewGuid(),
                        Name = serviceDto.Name,
                        Description = serviceDto.Description,
                        Price = serviceDto.Price,
                        Currency = normalizedCurrency,
                        Duration = serviceDto.Duration,
                        BusinessId = businessId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _context.Services.AddAsync(newService);
                }
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Usluge za biznis entitet ažurirane uspešno");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom ažuriranja usluga za biznis entitet");
        return false;
    }
}

public async Task<IEnumerable<BusinessByCategoryDto>> GetBusinessesByCategory(Guid categoryId)
{
    try
    {
        var businesses = await _context.Businesses
            .Include(b => b.Categories)
            .Include(b => b.Addresses)
            .Where(b => b.Categories!.Any(c => c.Id == categoryId) && b.IsActive == true)
            .Select(b => new BusinessByCategoryDto
            {
                Id = b.Id,
                Name = b.Name,
                ImageUrls = b.ImageUrls,
                City = b.Addresses != null && b.Addresses.Any(x => x.City != null) ? b.Addresses.First(x => x.City != null).City : null,
                Address = b.Addresses != null && b.Addresses.Any(x => x.StreetName != null) ? b.Addresses.First(x => x.StreetName != null).StreetName : null
            })
            .ToListAsync();

        _logger.LogInformation("Pronadjeno {Count} biznis entiteta za kategoriju ", 
            businesses.Count);
        return businesses;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom pronalaženja biznis entiteta za kategoriju");
        return Enumerable.Empty<BusinessByCategoryDto>();
    }
}

public async Task<BusinessDetailsResponse?> GetBusinessDetails(Guid businessId)
{
    try
    {
        var business = await _dbSet
            .Include(b => b.Categories)
            .Include(b => b.Services)
            .Include(b => b.Employees)
            .Include(b => b.Addresses)
            .FirstOrDefaultAsync(b => b.Id == businessId);

        if (business == null)
        {
            _logger.LogWarning("Biznis entitet  nije pronađen");
            return null;
        }

        var categoriesWithServices = business.Categories?
            .Select(c => new CategoryWithServices
            {
                Id = c.Id,
                Name = c.Name,
                Services = business.Services?
                    .Where(s => s.CategoryId == c.Id)
                    .Select(s => new ServiceDetails
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Description = s.Description,
                        Price = s.Price ?? 0,
                        Currency = string.IsNullOrWhiteSpace(s.Currency) ? null : s.Currency,
                        Duration = s.Duration ?? TimeSpan.Zero,
                        ImageUrl = s.ImageUrl
                    })
                    .ToList() ?? new List<ServiceDetails>()
            })
            .ToList();

        var address = business.Addresses?.FirstOrDefault();

        return new BusinessDetailsResponse
        {
            Id = business.Id,
            Name = business.Name,
            Description = business.Description,
            Images = business.ImageUrls,
            StartWorkHours = business.StartWorkHours,
            EndWorkHours = business.EndWorkHours,
            PIB = business.PIB,
            PhoneNumber = business.PhoneNumber,
            IsActive = business.IsActive ?? false,
            Categories = categoriesWithServices,
            Employees = business.Employees?
                .Select(e => new BusinessEmployeeDto
                {
                    Id = e.Id,
                    Name = $"{e.FirstName} {e.LastName}".Trim(),
                    ImageUrl = e.ImageUrl,
                    IsAvailable = e.IsAvailable ?? false
                })
                .ToList(),
            CreatedAt = business.CreatedAt,
            UpdatedAt = business.UpdatedAt,
            Address = address?.StreetName,
            City = address?.City,
            Country = address?.State,
            PostalCode = address?.ZipCode,
            Longitude = address?.Longitude ?? 0,
            Latitude = address?.Latitude ?? 0,
            Ticker = business.Tick?.ToString() ?? string.Empty
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom pronalaženja detalja biznis entiteta");
        return null;
    }
}

public async Task<bool> AddEmployeeToBusiness(Guid businessId, Employee employee)
{
    try
    {
        var business = await _dbSet
            .Include(b => b.Employees)
            .FirstOrDefaultAsync(b => b.Id == businessId);

        if (business == null)
        {
            _logger.LogWarning("Biznis entitet  nije pronađen");
            return false;
        }

        // Initialize Employees collection if null
        business.Employees ??= new List<Employee>();

        // Set up the employee
        employee.BusinessId = businessId;
        employee.CreatedAt = DateTime.UtcNow;
        employee.UpdatedAt = DateTime.UtcNow;
        
        // Add employee directly to the Employees DbSet
        await _context.Set<Employee>().AddAsync(employee);
        
        // Add to business's collection
        business.Employees.Add(employee);
        
        // Save changes
        await _context.SaveChangesAsync();

        _logger.LogInformation("Zaposleni uspešno dodat biznis entitetu");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom dodavanja zaposlenog biznis entitetu");
        return false;
    }
}

public async Task<IEnumerable<EmployeeResponse>> GetBusinessEmployees(Guid businessId)
{
    try
    {
        var business = await _dbSet
            .Include(b => b.Employees)
            .FirstOrDefaultAsync(b => b.Id == businessId);

        if (business?.Employees == null)
            return Enumerable.Empty<EmployeeResponse>();

        return business.Employees.Select(e => new EmployeeResponse
        {
            Id = e.Id,
            Name = e.FirstName ?? "",
            LastName = e.LastName ?? "",
            ImageUrl = e.ImageUrl ?? "",
            IsAvailable = e.IsAvailable ?? false
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom pronalaženja zaposlenih za biznis entitet");
        return Enumerable.Empty<EmployeeResponse>();
    }
}

    public async Task<AvailableTimeSlotsResponse> GetAvailableTimeSlots(Guid businessId, Guid employeeId, Guid serviceId, DateTime date)
{
    try
    {
         var utcDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);
        
        var schedule = await _context.BusinessSchedules
            .Include(b => b.Business.Employees)
            .Where(b => b.Date.Date == date.Date)
            .FirstOrDefaultAsync(b => b.BusinessId == businessId);

        if (schedule == null || schedule.IsWorkingDay == false)
        {
            _logger.LogWarning("Biznis entitet nije radni dan ili nema radnog vremena");
            return new AvailableTimeSlotsResponse { Date = utcDate };
        }

        // Get business, employee, and service details
        var business = await _dbSet
            .Include(b => b.Employees)
            .FirstOrDefaultAsync(b => b.Id == businessId);

        if (business == null)
        {
            _logger.LogWarning("Biznis entitet nije pronađen");
            return new AvailableTimeSlotsResponse { Date = utcDate };
        }

        var employee = await _context.Employees
            .Include(e => e.Appointments)
            .FirstOrDefaultAsync(e => e.Id == employeeId);
        
        if (employee == null || employee.IsAvailable == false)
        {
            _logger.LogWarning("Zaposleni nije dostupan ili nije pronađen");
            return new AvailableTimeSlotsResponse { Date = utcDate };
        }

        var service = await _context.Services
            .FirstOrDefaultAsync(s => s.Id == serviceId);

        if (service == null || service.Duration == null)
        {
            _logger.LogWarning("Usluga nije pronađena ili nema trajanja");
            return new AvailableTimeSlotsResponse { Date = utcDate };
        }

        if (business == null || employee == null)
        {
            _logger.LogWarning("Neispravni podaci biznis entiteta, zaposlenog ili usluge");
            return new AvailableTimeSlotsResponse { Date = utcDate };
        }

        // Check if business is closed on this date
        if (schedule != null && !schedule.IsWorkingDay)
        {
            return new AvailableTimeSlotsResponse 
            { 
                Date = utcDate,
                AvailableTimeSlots = new List<TimeSpan>(),
                //Message = "Business is closed on this date"
            };
        }

        var workStartTime = schedule?.CustomStartTime ?? TimeSpan.Zero;
        var workEndTime = schedule?.CustomEndTime ?? TimeSpan.FromHours(24);

        // Get existing appointments for the employee on the specified date
        var existingAppointments = await _context.Appointments
            .Where(a => 
                a.EmployeeId == employeeId &&
                a.AppointmentDate != null &&
                a.AppointmentDate.Value.Date == utcDate.Date &&
                (a.IsCancelled == false || a.IsCancelled == null)
            )
            .ToListAsync();

        // Filter out appointments without valid times in memory
        existingAppointments = existingAppointments
            .Where(a => a.StartTime != null && a.EndTime != null)
            .ToList();

        // Get free time slots and order them in memory
        var freeTimeSlots = await _context.FreeTimeSlots
            .Where(f => 
                f.EmployeeId == employeeId && 
                f.Date.Date == utcDate.Date
            )
            .ToListAsync();

        // Order in memory instead of database
        freeTimeSlots = freeTimeSlots
            .OrderBy(f => f.StartTime.TotalMinutes)
            .ToList();

        // Combine custom schedule hours with free time slots
        var availableTimeRanges = new List<(TimeSpan Start, TimeSpan End)>
        {
            // Use custom schedule times
            (workStartTime, workEndTime)
        };

        // Generate time slots based on service duration
        var availableSlots = new List<TimeSpan>();
        var serviceDuration = service.Duration.Value;

        foreach (var timeRange in availableTimeRanges)
        {
            var slotStart = timeRange.Start;
            // Get interval from business.Tick or default to 30 minutes
            var interval = business.Tick != default(TimeSpan) && business.Tick != TimeSpan.Zero
                ? business.Tick
                : TimeSpan.FromMinutes(30);

            while (slotStart < timeRange.End)
            {
                bool isSlotAvailable = true;

                // First check if the slot overlaps with any free time slot
                foreach (var freeSlot in freeTimeSlots)
                {
                    if (IsTimeSlotOverlapping(slotStart, slotStart.Add(interval ?? TimeSpan.FromMinutes(30)), freeSlot.StartTime, freeSlot.EndTime))
                    {
                        isSlotAvailable = false;
                        break;
                    }
                }

                // Only check appointments if the slot hasn't already been marked as unavailable
                if (isSlotAvailable)
                {
                    foreach (var appointment in existingAppointments)
                    {
                        if (appointment.StartTime.HasValue && 
                            appointment.EndTime.HasValue && 
                            IsTimeSlotOverlapping(slotStart, slotStart.Add(interval ?? TimeSpan.FromMinutes(30)), appointment.StartTime.Value, appointment.EndTime.Value))
                        {
                            isSlotAvailable = false;
                            break;
                        }
                    }
                }

                if (isSlotAvailable)
                {
                    availableSlots.Add(slotStart);
                }

                // Move to the next slot based on the interval
                slotStart = slotStart.Add(interval ?? TimeSpan.FromMinutes(30));
            }
        }

        return new AvailableTimeSlotsResponse 
        { 
            Date = utcDate,
            AvailableTimeSlots = availableSlots 
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom pronalaženja dostupnih vremenskih slotova");
        return new AvailableTimeSlotsResponse { Date = date };
    }
}

private bool IsTimeSlotOverlapping(TimeSpan newStart, TimeSpan newEnd, TimeSpan existingStart, TimeSpan existingEnd)
{
    return newStart < existingEnd && existingStart < newEnd;
}

public async Task<bool> UpdateBusinessHours(Guid businessId, TimeSpan startWorkHours, TimeSpan endWorkHours, TimeSpan? tick)
{
    try
    {
        var business = await _dbSet.FindAsync(businessId);
        
        if (business == null)
        {
            _logger.LogWarning("Biznis entitet  nije pronađen");
            return false;
        }

        if (startWorkHours >= endWorkHours)
        {
            _logger.LogWarning("Neispravno radno vreme: početno vreme mora biti pre krajnjeg vremena");
            return false;
        }

        business.StartWorkHours = startWorkHours;
        business.EndWorkHours = endWorkHours;
        business.Tick = tick ?? TimeSpan.FromMinutes(30); // Default to 30 minutes if not specified
        business.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Uspešno ažurirano radno vreme i interval za biznis entitet");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom ažuriranja radnog vremena za biznis entitet");
        return false;
    }
}

public async Task<bool> ToggleBusinessActiveStatusAsync(Guid businessId, bool isActive)
{
    try
    {
        var business = await _dbSet.FindAsync(businessId);
        if (business == null)
        {
            _logger.LogWarning("Biznis entitet  nije pronađen");
            return false;
        }

        business.IsActive = isActive;
        business.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Status aktivnosti biznis entiteta ažuriran na");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom ažuriranja statusa aktivnosti za biznis entitet");
        return false;
    }
}

public async Task<CreateBussinessResponce?> UpdateBusinessImagesAsync(Guid businessId, List<string> imagePaths)
{
    try
    {
        var business = await _dbSet.FindAsync(businessId);
        if (business == null)
        {
            _logger.LogWarning("Biznis entitet  nije pronađen");
            return null;
        }

        business.ImageUrls = imagePaths;
        business.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new CreateBussinessResponce
        {
            Id = business.Id,
            Name = business.Name,
            Description = business.Description,
            Images = business.ImageUrls,
            CreatedAt = business.CreatedAt,
            UpdatedAt = business.UpdatedAt,
            IsActive = business.IsActive ?? false
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom ažuriranja slika za biznis entitet");
        return null;
    }
}

public async Task<bool> AddFreeTimeSlotAsync(Guid businessId, Guid employeeId, DateTime date, TimeSpan startTime, TimeSpan endTime, string? note = null, string? serviceName = null)
{
    try
    {
        var business = await _dbSet
            .Include(b => b.Employees)
            .FirstOrDefaultAsync(b => b.Id == businessId);

        if (business == null || business.Employees == null || !business.Employees.Any(e => e.Id == employeeId))
        {
            _logger.LogWarning("Biznis entitet ili zaposleni nije pronađen");
            return false;
        }



        // Get existing appointments for the employee on the specified date
        var existingAppointments = await _context.Appointments
            .Where(a => 
                a.EmployeeId == employeeId &&
                a.AppointmentDate != null &&
                a.AppointmentDate.Value.Date == date.Date &&
                (a.IsCancelled == false || a.IsCancelled == null)
            )
            .ToListAsync();

        // Filter out appointments without valid times in memory
        existingAppointments = existingAppointments
            .Where(a => a.StartTime != null && a.EndTime != null)
            .ToList();

        // Get free time slots and order them in memory
        var freeTimeSlots = await _context.FreeTimeSlots
            .Where(f => 
                f.EmployeeId == employeeId && 
                f.Date.Date == date.Date
            )
            .ToListAsync();

        // Order in memory instead of database
        freeTimeSlots = freeTimeSlots
            .OrderBy(f => f.StartTime.TotalMinutes)
            .ToList();

        // Check for conflicts in memory
        var hasConflicts = existingAppointments.Any(a =>
            a.StartTime.HasValue && a.EndTime.HasValue && (
                (a.StartTime.Value <= startTime && a.EndTime.Value > startTime) ||
                (a.StartTime.Value < endTime && a.EndTime.Value >= endTime) ||
                (startTime <= a.StartTime.Value && endTime > a.StartTime.Value)
            )
        );

        if (hasConflicts)
        {
            _logger.LogWarning("Nalazi se konflikt termina");
            return false;
        }

        var freeTimeSlot = new FreeTimeSlot
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EmployeeId = employeeId,
            Date = date,
            StartTime = startTime,
            EndTime = endTime,
            Note = note,
            ServiceName = serviceName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.FreeTimeSlots.AddAsync(freeTimeSlot);
        await _context.SaveChangesAsync();
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom dodavanja slobodnog vremenskog slota");
        return false;
    }
}

public async Task<bool> AddFreeTimeSlotRangeAsync(Guid businessId, Guid employeeId, DateTime startDate, DateTime endDate, TimeSpan startTime, TimeSpan endTime, string? note = null, string? serviceName = null)
{
    try
    {
        // Ensure dates are in UTC
        var utcStartDate = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var utcEndDate = DateTime.SpecifyKind(endDate.Date, DateTimeKind.Utc);

        var business = await _dbSet
            .Include(b => b.Employees)
            .FirstOrDefaultAsync(b => b.Id == businessId);

        if (business == null || business.Employees == null || !business.Employees.Any(e => e.Id == employeeId))
        {
            _logger.LogWarning("Biznis entitet ili zaposleni nije pronađen");
            return false;
        }

        // Validate date range
        if (startDate > endDate)
        {
            _logger.LogWarning("Neispravan opseg datuma: početni datum mora biti pre krajnjeg datuma");
            return false;
        }

        // Get business schedule for the specific date range
        var schedule = await _context.BusinessSchedules
            .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.Date.Date == startDate.Date);

        // Validate time against custom schedule only
        var workStartTime = schedule?.CustomStartTime ?? TimeSpan.Zero;
        var workEndTime = schedule?.CustomEndTime ?? TimeSpan.FromHours(24);

        if (startTime < workStartTime || endTime > workEndTime)
        {
            _logger.LogWarning("Vremenski slot izvan radnog vremena");
            return false;
        }

        // Generate all dates in the range
        var dates = Enumerable.Range(0, (utcEndDate - utcStartDate).Days + 1)
            .Select(offset => utcStartDate.AddDays(offset))
            .ToList();

        var freeTimeSlotsToAdd = new List<FreeTimeSlot>();

        foreach (var date in dates)
        {
            // Get existing appointments for the employee on the specified date
            var existingAppointments = await _context.Appointments
                .Where(a => 
                    a.EmployeeId == employeeId &&
                    a.AppointmentDate != null &&
                    a.AppointmentDate.Value.Date == date.Date &&
                    (a.IsCancelled == false || a.IsCancelled == null)
                )
                .ToListAsync();

            // Filter out appointments without valid times in memory
            existingAppointments = existingAppointments
                .Where(a => a.StartTime != null && a.EndTime != null)
                .ToList();

            // Get existing free time slots for the employee on the specified date
            var existingFreeTimeSlots = await _context.FreeTimeSlots
                .Where(f => 
                    f.EmployeeId == employeeId && 
                    f.Date.Date == date.Date
                )
                .ToListAsync();

            // Check for conflicts with appointments
            var hasAppointmentConflicts = existingAppointments.Any(a =>
                a.StartTime.HasValue && a.EndTime.HasValue && (
                    (a.StartTime.Value <= startTime && a.EndTime.Value > startTime) ||
                    (a.StartTime.Value < endTime && a.EndTime.Value >= endTime) ||
                    (startTime <= a.StartTime.Value && endTime > a.StartTime.Value)
                )
            );

            if (hasAppointmentConflicts)
            {
                _logger.LogWarning("Nalazi se konflikt termina za datum {Date}", date.ToShortDateString());
                continue; // Skip this date but continue with others
            }

            // Check for conflicts with existing free time slots
            var hasFreeTimeConflicts = existingFreeTimeSlots.Any(f =>
                (f.StartTime <= startTime && f.EndTime > startTime) ||
                (f.StartTime < endTime && f.EndTime >= endTime) ||
                (startTime <= f.StartTime && endTime > f.StartTime)
            );

            if (hasFreeTimeConflicts)
            {
                _logger.LogWarning("Nalazi se konflikt sa postojećim slobodnim terminom za datum {Date}", date.ToShortDateString());
                continue; // Skip this date but continue with others
            }

            // Create free time slot for this date
            var freeTimeSlot = new FreeTimeSlot
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                EmployeeId = employeeId,
                Date = date.Date, // Ensure we store only the date part
                StartTime = startTime,
                EndTime = endTime,
                Note = note,
                ServiceName = serviceName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            freeTimeSlotsToAdd.Add(freeTimeSlot);
            _logger.LogDebug("Added free time slot for date {Date} with time {StartTime}-{EndTime}", date.Date, startTime, endTime);
        }

        if (freeTimeSlotsToAdd.Any())
        {
            await _context.FreeTimeSlots.AddRangeAsync(freeTimeSlotsToAdd);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Uspešno dodato {Count} slobodnih vremenskih slotova za period od {StartDate} do {EndDate}", 
                freeTimeSlotsToAdd.Count, startDate.ToShortDateString(), endDate.ToShortDateString());
            return true;
        }
        else
        {
            _logger.LogWarning("Nije dodao nijedan slobodan vremenski slot zbog konflikata");
            return false;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom dodavanja slobodnih vremenskih slotova za period");
        return false;
    }
}

        public async Task<bool> DeleteFreeTimeSlotAsync(Guid businessId, Guid freeTimeSlotId)
    {
        try
        {
            // Find the free time slot and verify it belongs to the business
            var freeTimeSlot = await _context.FreeTimeSlots
                .FirstOrDefaultAsync(f => f.Id == freeTimeSlotId && f.BusinessId == businessId);

            if (freeTimeSlot == null)
            {
                _logger.LogWarning("Slobodan vremenski slot nije pronađen ili ne pripada biznisu");
                return false;
            }

            // Remove the free time slot
            _context.FreeTimeSlots.Remove(freeTimeSlot);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Slobodan vremenski slot je uspešno obrisan. ID: {FreeTimeSlotId}, Biznis: {BusinessId}", 
                freeTimeSlotId, businessId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom brisanja slobodnog vremenskog slota");
            return false;
        }
    }

    public async Task<bool> DeleteFreeTimeSlotRangeAsync(Guid businessId, Guid employeeId, DateTime startDate, DateTime endDate)
    {
        try
        {
            // Validate input parameters
            if (startDate > endDate)
            {
                _logger.LogWarning("Početni datum mora biti pre krajnjeg datuma");
                return false;
            }

            // Ensure dates are in UTC
            var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

            _logger.LogInformation("Brisanje slobodnih vremenskih slotova za period od {StartDate} do {EndDate}", 
                utcStartDate.Date, utcEndDate.Date);

            // Find all free time slots for the business, employee, and date range
            var freeTimeSlotsToDelete = await _context.FreeTimeSlots
                .Where(f => 
                    f.BusinessId == businessId &&
                    f.EmployeeId == employeeId &&
                    f.Date.Date >= utcStartDate.Date &&
                    f.Date.Date <= utcEndDate.Date
                )
                .ToListAsync();

            if (!freeTimeSlotsToDelete.Any())
            {
                _logger.LogInformation("Nema slobodnih vremenskih slotova za brisanje u navedenom periodu");
                return true; // Not an error - just nothing to delete
            }

            _logger.LogInformation("Pronađeno {Count} slobodnih vremenskih slotova za brisanje", freeTimeSlotsToDelete.Count);

            // Remove all found free time slots
            _context.FreeTimeSlots.RemoveRange(freeTimeSlotsToDelete);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Uspešno obrisano {Count} slobodnih vremenskih slotova za period od {StartDate} do {EndDate}", 
                freeTimeSlotsToDelete.Count, utcStartDate.Date, utcEndDate.Date);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom brisanja slobodnih vremenskih slotova za period");
            return false;
        }
    }

public async Task<IEnumerable<FreeTimeSlotResponse>> GetFreeTimeSlotsForDate(Guid businessId, Guid employeeId, DateTime date)
{
    try
    {
        var freeTimeSlots = await _context.FreeTimeSlots
            .Where(f => 
                f.BusinessId == businessId &&
                f.EmployeeId == employeeId && 
                f.Date.Date == date.Date
            )
            .ToListAsync();

        _logger.LogInformation("Pronadjeno {Count} slobodnih vremenskih slotova", freeTimeSlots.Count);
        
        foreach (var slot in freeTimeSlots)
        {
            _logger.LogInformation(
                "Slobodan slot - Datum: {Date}, Početak: {Start}, Kraj: {End}", 
                slot.Date, slot.StartTime, slot.EndTime);
        }

        return freeTimeSlots.Select(f => new FreeTimeSlotResponse
        {
            Id = f.Id,
            Date = f.Date,
            StartTime = f.StartTime,
            EndTime = f.EndTime,
            EmployeeId = f.EmployeeId,
            BusinessId = f.BusinessId,
            Note = f.Note,
            ServiceName = f.ServiceName
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom pronalaženja slobodnih vremenskih slotova");
        return Enumerable.Empty<FreeTimeSlotResponse>();
    }
}

public async Task<IEnumerable<FreeTimeSlotResponse>> GetFreeTimeSlotsForDateRange(Guid businessId, Guid employeeId, DateTime startDate, DateTime endDate)
{
    try
    {
        // Ensure dates are in UTC
        var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
        var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

        var freeTimeSlots = await _context.FreeTimeSlots
            .Where(f => 
                f.BusinessId == businessId &&
                f.EmployeeId == employeeId && 
                f.Date.Date >= utcStartDate.Date &&
                f.Date.Date <= utcEndDate.Date
            )
            .OrderBy(f => f.Date)
            .ThenBy(f => f.StartTime)
            .ToListAsync();

        _logger.LogInformation("Pronadjeno {Count} slobodnih vremenskih slotova za period od {StartDate} do {EndDate}", 
            freeTimeSlots.Count, utcStartDate.Date, utcEndDate.Date);
        
        foreach (var slot in freeTimeSlots)
        {
            _logger.LogInformation(
                "Slobodan slot - Datum: {Date}, Početak: {Start}, Kraj: {End}", 
                slot.Date, slot.StartTime, slot.EndTime);
        }

        return freeTimeSlots.Select(f => new FreeTimeSlotResponse
        {
            Id = f.Id,
            Date = f.Date,
            StartTime = f.StartTime,
            EndTime = f.EndTime,
            EmployeeId = f.EmployeeId,
            BusinessId = f.BusinessId,
            Note = f.Note,
            ServiceName = f.ServiceName
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom pronalaženja slobodnih vremenskih slotova za period");
        return Enumerable.Empty<FreeTimeSlotResponse>();
    }
}

public async Task<bool> DeleteBusinessImageAsync(Guid businessId, string imagePath)
{
    try
    {
        var business = await _dbSet.FindAsync(businessId);
        if (business == null || business.ImageUrls == null)
        {
            _logger.LogWarning("Biznis entitet  nije pronađen");
            return false;
        }

        if (!business.ImageUrls.Contains(imagePath))
        {
            _logger.LogWarning("Slika nije pronađena u biznis entitetu");
            return false;
        }

        business.ImageUrls.Remove(imagePath);
        business.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Slika obrisana iz biznis entiteta");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom brisanja slike za biznis entitet");
        return false;
    }
}

public async Task<bool> AddBusinessAddress(Guid businessId, Address address,string? PIB,string? phoneNumber)
{
    try
    {
        var business = await _dbSet
            .Include(b => b.Addresses)
            .FirstOrDefaultAsync(b => b.Id == businessId);

        if (business == null)
        {
            _logger.LogWarning("Biznis entitet  nije pronađen");
            return false;
        }

        // Initialize Addresses collection if null
        business.Addresses ??= new List<Address>();

        // Set up the address
        address.BusinessId = businessId;
        address.CreatedAt = DateTime.UtcNow;
        address.UpdatedAt = DateTime.UtcNow;

        if(PIB != null && phoneNumber != null){
            business.PIB = PIB;
            business.PhoneNumber = phoneNumber;
        }
        
        // Add address to the Addresses DbSet
        await _context.Set<Address>().AddAsync(address);
        
        // Add to business's collection
        business.Addresses.Add(address);
        
        await _context.SaveChangesAsync();

        _logger.LogInformation("Adresa uspešno dodata biznis entitetu");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom dodavanja adrese biznis entitetu");
        return false;
    }
}

public async Task<bool> UpdateBusinessContactInfo(Guid businessId, string? pib, string? phoneNumber)
{
    try
    {
        var business = await _dbSet.FindAsync(businessId);
        if (business == null)
        {
            _logger.LogWarning("Biznis entitet  nije pronađen");
            return false;
        }

        if (pib != null)
            business.PIB = pib;
        
        if (phoneNumber != null)
            business.PhoneNumber = phoneNumber;

        business.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Uspešno ažurirana kontakt informacija za biznis entitet");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom ažuriranja kontakt informacija za biznis entitet");
        return false;
    }
}

public async Task<IEnumerable<NearbyBusinessResponse>> GetNearbyBusinesses(double latitude, double longitude, int limit = 5)
{
    try
    {
        // First, get all businesses with their addresses
        var businesses = await _dbSet
            .Include(b => b.Addresses)
            .Where(b => b.Addresses != null && b.Addresses.Any() && b.IsActive == true)
            .Select(b => new
            {
                Business = b,
                Address = b.Addresses != null ? b.Addresses.FirstOrDefault() : null
            })
            .ToListAsync();

        // Calculate distances in memory and order
        var nearbyBusinesses = businesses
            .Select(b => new
            {
                Business = b.Business,
                Address = b.Address,
                Distance = CalculateDistance(
                    latitude,
                    longitude,
                    b.Address?.Latitude ?? 0,
                    b.Address?.Longitude ?? 0)
            })
            .Where(b => b.Address?.Latitude != null && b.Address?.Longitude != null)
            .OrderBy(b => b.Distance)
            .Take(limit);

        // Map to response
        return nearbyBusinesses.Select(b => new NearbyBusinessResponse
        {
            Id = b.Business.Id,
            Name = b.Business.Name,
            Description = b.Business.Description,
            ImageUrls = b.Business.ImageUrls,
            Distance = Math.Round(b.Distance, 2),
            Latitude = b.Address?.Latitude ?? 0,
            Longitude = b.Address?.Longitude ?? 0,
            Address = b.Address?.StreetName,
            City = b.Address?.City
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom pronalaženja blizih biznis entiteta");
        return Enumerable.Empty<NearbyBusinessResponse>();
    }
}

private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
{
    const double earthRadius = 6371; // Earth's radius in kilometers

    var dLat = ToRadian(lat2 - lat1);
    var dLon = ToRadian(lon2 - lon1);

    var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRadian(lat1)) * Math.Cos(ToRadian(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

    var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    return earthRadius * c;
}

private static double ToRadian(double degree)
{
    return degree * Math.PI / 180;
}

public async Task<IEnumerable<CategoryWithServices>> GetBusinessCategoriesWithServices(Guid businessId)
{
    try
    {
        var business = await _dbSet
            .Include(b => b.Categories)
            .Include(b => b.Services)
            .FirstOrDefaultAsync(b => b.Id == businessId);

        if (business == null)
        {
            _logger.LogWarning("Biznis entitet  nije pronađen");
            return Enumerable.Empty<CategoryWithServices>();
        }

        var categoriesWithServices = business.Categories?
            .Select(category => new CategoryWithServices
            {
                Id = category.Id,
                Name = category.Name,
                Services = business.Services?
                    .Where(s => s.CategoryId == category.Id)
                    .Select(s => new ServiceDetails
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Description = s.Description,
                        Price = s.Price ?? 0,
                        Currency = string.IsNullOrWhiteSpace(s.Currency) ? null : s.Currency,
                        Duration = s.Duration ?? TimeSpan.Zero,
                        ImageUrl = s.ImageUrl
                    })
                    .ToList() ?? new List<ServiceDetails>()
            })
            .ToList() ?? new List<CategoryWithServices>();

        _logger.LogInformation("Pronadjeno {Count} kategorija sa uslugama za biznis entitet", 
            categoriesWithServices.Count);
        return categoriesWithServices;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Greška prilikom pronalaženja kategorija sa uslugama za biznis entitet");
        return Enumerable.Empty<CategoryWithServices>();
    }
}


    public async Task<bool> SetBusinessScheduleRange(Guid businessId, BusinessScheduleRangeRequest request)
    {
        try
        {
            var dates = Enumerable.Range(0, (request.EndDate - request.StartDate).Days + 1)
                .Select(offset => request.StartDate.AddDays(offset));

            foreach (var date in dates)
            {
                var schedule = await _context.BusinessSchedules
                    .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.Date.Date == date.Date);

                if(schedule == null)
                {
                    schedule = new BusinessSchedule
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        Date = date.Date,
                        DayOfWeek = date.DayOfWeek,
                        CreatedAt = DateTime.UtcNow,
                        IsWorkingDay = !request.NonWorkingDays.Contains(date.DayOfWeek)
                    };
                    await _context.BusinessSchedules.AddAsync(schedule);
                }

                if (request.NonWorkingDays.Contains(date.DayOfWeek))
                {
                    schedule.BusinessId = businessId;
                    schedule.Date = date.Date;
                    schedule.DayOfWeek = date.DayOfWeek;
                    schedule.CreatedAt = DateTime.UtcNow;
                    schedule.IsWorkingDay = false;
                }
                else
                {
                    schedule.BusinessId = businessId;
                    schedule.Date = date.Date;
                    schedule.DayOfWeek = date.DayOfWeek;
                    schedule.CreatedAt = DateTime.UtcNow;
                    schedule.IsWorkingDay = true;
                }
                
                schedule.CustomStartTime = request.DefaultStartTime;
                schedule.CustomEndTime = request.DefaultEndTime;
                schedule.UpdatedAt = DateTime.UtcNow;
                
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom postavljanja opsega radnog vremena za biznis entitet");
            return false;
        }
    }

    public async Task<bool> SetBusinessWeeklyTemplateRange(Guid businessId, SetBusinessWeeklyTemplateRangeRequest request)
    {
        try
        {
            // Validate request
            if (request.StartDate > request.EndDate)
            {
                _logger.LogWarning("Start date is after end date for business");
                return false;
            }

            if (request.WorkingDays == null || !request.WorkingDays.Any())
            {
                _logger.LogWarning("No working days provided for business");
                return false;
            }

            // Validate that all days of the week are covered
            var providedDays = request.WorkingDays.Select(wd => wd.DayOfWeek).Distinct().ToList();
            var allDaysOfWeek = Enum.GetValues<DayOfWeek>().ToList();
            
            if (providedDays.Count != allDaysOfWeek.Count)
            {
                _logger.LogWarning("Not all days of the week are provided for business");
            }

            // Ensure dates are in UTC
            var startDate = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(request.EndDate.Date, DateTimeKind.Utc);

            var dates = Enumerable.Range(0, (endDate - startDate).Days + 1)
                .Select(offset => startDate.AddDays(offset));
    
            foreach (var date in dates)
            {
                _logger.LogDebug("Processing date {Date} with DayOfWeek {DayOfWeek}", date.Date, date.DayOfWeek);
                
                var template = request.WorkingDays.FirstOrDefault(wd => wd.DayOfWeek == date.DayOfWeek);
                if (template == null) 
                {
                    _logger.LogDebug("No template found for day {DayOfWeek} on date {Date}.", 
                        date.DayOfWeek, date);
                    continue;
                }
    
                var schedule = await _context.BusinessSchedules
                    .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.Date.Date == date.Date);
    
                if (schedule == null)
                {
                    schedule = new BusinessSchedule
                    {
                        Id = Guid.NewGuid(),
                        BusinessId = businessId,
                        Date = date.Date,
                        DayOfWeek = date.DayOfWeek,
                        IsWorkingDay = template.IsWorkingDay,
                        CustomStartTime = template.StartTime,
                        CustomEndTime = template.EndTime,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _context.BusinessSchedules.AddAsync(schedule);
                    _logger.LogDebug("Created new schedule for business on {Date} with DayOfWeek {DayOfWeek}", date.Date, date.DayOfWeek);
                }
                else
                {
                    // Update existing schedule including DayOfWeek
                    schedule.DayOfWeek = date.DayOfWeek;
                    schedule.IsWorkingDay = template.IsWorkingDay;
                    schedule.CustomStartTime = template.StartTime;
                    schedule.CustomEndTime = template.EndTime;
                    schedule.UpdatedAt = DateTime.UtcNow;
                    _logger.LogDebug("Updated existing schedule for business on {Date} with DayOfWeek {DayOfWeek}", date.Date, date.DayOfWeek);
                }
            }
    
            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully set weekly template range for business from {StartDate} to {EndDate}", startDate, endDate);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting business weekly template range for business");
            return false;
        }
    }

    public async Task<IEnumerable<BusinessSchedule>> GetBusinessSchedule(Guid businessId, DateTime startDate, DateTime endDate)
    {
        try
        {
            // Ensure dates are in UTC
            var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

            var schedules = await _context.BusinessSchedules
                .Where(b => b.BusinessId == businessId && 
                           b.Date.Date >= utcStartDate.Date && 
                           b.Date.Date <= utcEndDate.Date)
                .OrderBy(b => b.Date)
                .ToListAsync();

            _logger.LogInformation("Pronadjeno {Count} rasporeda za biznis", schedules.Count);
            return schedules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom pronalaženja rasporeda za biznis");
            throw;
        }
    }

    public async Task<bool> IsBusinessAvailableOnDate(Guid businessId, DateTime date)
    {
        try
        {
            // Convert the date to UTC if it's not already
            var utcDate = date.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
                : date.ToUniversalTime();

            var schedule = await _context.BusinessSchedules
                .FirstOrDefaultAsync(b => b.BusinessId == businessId && 
                    b.Date.Date == utcDate.Date);

            return schedule?.IsWorkingDay ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom proveravanja dostupnosti biznis entiteta");
            return false;
        }
    }

    public async Task<bool> SetBusinessSchedule(Guid businessId, BusinessScheduleRequest request)
    {
        try
        {
            var schedule = await _context.BusinessSchedules
                .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.Date.Date == request.Date.Date);

            if (schedule == null)
            {
                schedule = new BusinessSchedule
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Date = request.Date.Date,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.BusinessSchedules.AddAsync(schedule);
            }

            schedule.IsWorkingDay = request.IsWorkingDay;
            schedule.CustomStartTime = request.CustomStartTime;
            schedule.CustomEndTime = request.CustomEndTime;
            schedule.Note = request.Note;
            schedule.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom postavljanja rasporeda za biznis entitet");
            return false;
        }
    }

    public async Task<bool> DeleteBusinessScheduleRange(Guid businessId, DateTime startDate, DateTime endDate)
    {
        try
        {
            // Ensure dates are in UTC
            var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

            var schedulesToDelete = await _context.BusinessSchedules
                .Where(s => s.BusinessId == businessId && 
                           s.Date.Date >= utcStartDate.Date && 
                           s.Date.Date <= utcEndDate.Date)
                .ToListAsync();

            if (schedulesToDelete.Any())
            {
                _context.BusinessSchedules.RemoveRange(schedulesToDelete);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Deleted {Count} business schedules for business from {StartDate} to {EndDate}", 
                    schedulesToDelete.Count, utcStartDate.Date, utcEndDate.Date);
            }
            else
            {
                _logger.LogInformation("No business schedules found to delete for business from {StartDate} to {EndDate}", utcStartDate.Date, utcEndDate.Date);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom brisanja rasporeda za biznis entitet od {StartDate} do {EndDate}", startDate, endDate);
            return false;
        }
    }

    public async Task<IEnumerable<BusinessByCategoryDto>> GetBusinessesByCategoryAndCity(Guid categoryId, string city)
    {
        try
        {
            var businesses = await _dbSet
                .Include(b => b.Categories)
                .Include(b => b.Addresses)
                .Where(b => b.Categories!.Any(c => c.Id == categoryId) &&
                           b.Addresses!.Any(a => a.City!.ToLower() == city.ToLower()))
                .Select(b => new BusinessByCategoryDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    ImageUrls = b.ImageUrls,
                    City = b.Addresses != null && b.Addresses.Any(x => x.City != null) ? b.Addresses.First(x => x.City != null).City : null,
                    Address = b.Addresses != null && b.Addresses.Any(x => x.StreetName != null) ? b.Addresses.First(x => x.StreetName != null).StreetName : null
                })
                .ToListAsync();

            _logger.LogInformation("Pronadjeno {Count} biznis entiteta za kategoriju {CategoryId} u gradu {City}", 
                businesses.Count, categoryId, city);
            return businesses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom pronalaženja biznis entiteta za kategoriju u gradu");
            return Enumerable.Empty<BusinessByCategoryDto>();
        }
    }

    public async Task<bool> RemoveBusinessCategories(Guid businessId, IEnumerable<Guid> categoryIdsToKeep)
    {
        try
        {
            var business = await _context.Set<Bussiness>()
                .Include(b => b.Categories)
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business?.Categories == null)
            {
                _logger.LogWarning("Biznis entitet  nije pronađen ili nema kategorija");
                return false;
            }

            var categoriesToRemove = business.Categories
                .Where(c => !categoryIdsToKeep.Contains(c.Id))
                .ToList();

            if (!categoriesToRemove.Any())
            {
                _logger.LogInformation("Nema kategorija za brisanje za biznis entitet");
                return true;
            }

            foreach (var category in categoriesToRemove)
            {
                business.Categories.Remove(category);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Obrisano {Count} kategorija iz biznis entiteta", 
                categoriesToRemove.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom brisanja kategorija iz biznis entiteta");
            return false;
        }
    }

} 

 

