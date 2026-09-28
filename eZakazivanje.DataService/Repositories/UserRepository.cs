using System;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Response;

namespace eZakazivanje.DataService.Repositories;

public class UserRepository : GenericRepository<ApplicationUser>, IUserRepository
{
    public UserRepository(AppDbContext context, ILogger logger) : base(context, logger){}

    public async Task<bool> UpdateImageAsync(Guid userId, string imagePath)
    {
        try
        {
            var userIdString = userId.ToString();
            var user = await _dbSet.FindAsync(userIdString);
            
            if (user == null)
            {
                _logger.LogWarning("User not found");
                return false;
            }

            user.ImageUrl = imagePath;
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("User image updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user image");
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        try
        {
            var userIdString = userId.ToString();
            var user = await _dbSet.FindAsync(userIdString);
            
            if (user == null)
            {
                _logger.LogWarning("User not found");
                return false;
            }

            // Delete user's appointments
            var userAppointments = await _context.Appointments
                .Where(a => a.ApplicationUserId == userIdString)
                .ToListAsync();
            
            _context.Appointments.RemoveRange(userAppointments);
            
            // Delete the user
            _dbSet.Remove(user);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("User and their appointments deleted successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user");
            return false;
        }
    }

    public async Task<BusinessUserDetailsDto?> GetBusinessUserDetailsByBusinessIdAsync(Guid businessId)
    {
        try
        {
            // First verify the business exists
            var businessExists = await _context.Businesses
                .AnyAsync(b => b.Id == businessId);

            if (!businessExists)
            {
                _logger.LogWarning("Business with ID {BusinessId} not found", businessId);
                return null;
            }

            // Get business information first to get UserId
            var business = await _context.Businesses
                .Include(b => b.Addresses)
                .Include(b => b.Categories)
                .Include(b => b.Services)
                .Include(b => b.Employees)
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business == null)
            {
                _logger.LogWarning("Business with ID {BusinessId} not found", businessId);
                return null;
            }

            // Get user information using UserId
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == business.UserId);

            if (user == null)
            {
                _logger.LogWarning("User associated with business ID {BusinessId} (UserId: {UserId}) not found", businessId, business.UserId);
                return null;
            }

            var businessDetails = new BusinessUserDetailsDto
            {
                UserId = user.Id,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                ImageUrl = user.ImageUrl ?? string.Empty,
                BusinessName = user.BusinessName ?? string.Empty,
                BusinessDescription = user.BusinessDescription ?? string.Empty,
                FCMToken = user.FCMToken ?? string.Empty,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Roles = new List<string>(), // Will be populated by controller using UserManager
                BusinessInfo = business != null ? new BusinessInfoDto
                {
                    BusinessId = business.Id,
                    Name = business.Name ?? string.Empty,
                    Description = business.Description ?? string.Empty,
                    IsActive = business.IsActive ?? true,
                    ApprovalStatus = business.ApprovalStatus.ToString(),
                    StartWorkHours = business.StartWorkHours ?? TimeSpan.Zero,
                    EndWorkHours = business.EndWorkHours ?? TimeSpan.Zero,
                    Tick = business.Tick,
                    CategoryName = business.Categories?.FirstOrDefault()?.Name ?? string.Empty,
                    Address = business.Addresses?.FirstOrDefault() != null ? new AddressDto
                    {
                        Street = business.Addresses.First().StreetName ?? string.Empty,
                        City = business.Addresses.First().City ?? string.Empty,
                        State = business.Addresses.First().State ?? string.Empty,
                        PostalCode = business.Addresses.First().ZipCode ?? string.Empty,
                        Country = string.Empty, // Not available in Address entity
                        Latitude = business.Addresses.First().Latitude,
                        Longitude = business.Addresses.First().Longitude
                    } : null,
                    ServicesCount = business.Services?.Count ?? 0,
                    EmployeesCount = business.Employees?.Count ?? 0,
                    Images = business.ImageUrls ?? new List<string>(),
                    PIB = business.PIB ?? string.Empty,
                    PhoneNumber = business.PhoneNumber ?? string.Empty,
                    CreatedAt = business.CreatedAt,
                    UpdatedAt = business.UpdatedAt
                } : null
            };

            _logger.LogInformation("Successfully retrieved business user details for business ID {BusinessId}", businessId);
            return businessDetails;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business user details for business ID {BusinessId}", businessId);
            return null;
        }
    }
}
