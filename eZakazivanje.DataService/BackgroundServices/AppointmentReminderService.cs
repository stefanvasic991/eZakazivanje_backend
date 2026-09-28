using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.BackgroundServices.Services;
using eZakazivanje.DataService.Data;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.DataService.Services;

namespace eZakazivanje.DataService.BackgroundServices;

public class AppointmentReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentReminderService> _logger;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public AppointmentReminderService(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentReminderService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppointmentReminderService je započeo rad");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Ensure only one instance runs at a time
                if (!await _semaphore.WaitAsync(0, stoppingToken))
                {
                    _logger.LogWarning("Prethodno proveravanje obaveštenja je još u toku, preskačem ovu iteraciju");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                try
                {
                    
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await DebugAppointmentData(dbContext);
                    var now = DateTime.UtcNow;

                    _logger.LogInformation("Počinjem proveru obaveštenja u {Time}", now);

                    // Test database connection
                    try
                    {
                        await dbContext.Database.OpenConnectionAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Greška prilikom povezivanja sa bazom podataka");
                        throw;
                    }

                    var baseQuery = dbContext.Appointments
                        .Include(a => a.ApplicationUser)
                        .Include(a => a.Business)
                        .Where(a => !a.IsCancelled.HasValue || !a.IsCancelled.Value)
                        .Where(a => a.ApplicationUser != null && a.Business != null)
                        .Select(a => new Appointment
                        {
                            Id = a.Id,
                            AppointmentDate = a.AppointmentDate,
                            StartTime = a.StartTime,
                            ApplicationUserId = a.ApplicationUserId,
                            Reminder24HoursSent = a.Reminder24HoursSent,
                            Reminder2HoursSent = a.Reminder2HoursSent,
                            Services = new List<Service>(),
                            ApplicationUser = new ApplicationUser
                            {
                                Id = a.ApplicationUser!.Id,
                                Email = a.ApplicationUser.Email,
                                FirstName = a.ApplicationUser.FirstName,
                                FCMToken = a.ApplicationUser.FCMToken,
                                PreferredLanguage = a.ApplicationUser.PreferredLanguage
                            },
                            Business = new Bussiness
                            {
                                Id = a.Business!.Id,
                                Name = a.Business.Name
                            }
                        });

                    // Get appointments for 24-hour reminder
                    var dailyReminders = await baseQuery
                        .Where(a => a.AppointmentDate.HasValue &&
                                   !a.Reminder24HoursSent.HasValue &&
                                   a.AppointmentDate.Value.Date == now.AddHours(2).AddDays(1).Date)
                        .ToListAsync(stoppingToken);

                    // Load services for daily reminders
                    foreach (var appointment in dailyReminders)
                    {
                        // Use raw SQL to avoid LINQ translation issues with many-to-many relationships
                        var services = await dbContext.Services
                            .FromSqlRaw(@"
                                SELECT s.""Id"", s.""Duration"" 
                                FROM ""Services"" s
                                INNER JOIN ""AppointmentService"" aps ON s.""Id"" = aps.""ServicesId""
                                WHERE aps.""AppointmentsId"" = {0}", appointment.Id)
                            .Select(s => new Service { Id = s.Id, Duration = s.Duration })
                            .ToListAsync(stoppingToken);
                        
                        appointment.Services = services;
                    }

                    _logger.LogInformation("Pronadjeno {Count} termina za 24-satno obaveštenje", dailyReminders.Count);

                    // Get appointments for 2-hour reminder
                    var twoHourReminders = await baseQuery
                        .Where(a => a.AppointmentDate.HasValue &&
                                   !a.Reminder2HoursSent.HasValue &&
                                   a.AppointmentDate.Value >= now.AddHours(2) &&
                                   a.AppointmentDate.Value <= now.AddHours(2).AddHours(2))
                        .ToListAsync(stoppingToken);

                    // Load services for two-hour reminders
                    foreach (var appointment in twoHourReminders)
                    {
                        // Use raw SQL to avoid LINQ translation issues with many-to-many relationships
                        var services = await dbContext.Services
                            .FromSqlRaw(@"
                                SELECT s.""Id"", s.""Duration"" 
                                FROM ""Services"" s
                                INNER JOIN ""AppointmentService"" aps ON s.""Id"" = aps.""ServicesId""
                                WHERE aps.""AppointmentsId"" = {0}", appointment.Id)
                            .Select(s => new Service { Id = s.Id, Duration = s.Duration })
                            .ToListAsync(stoppingToken);
                        
                        appointment.Services = services;
                    }

                    _logger.LogInformation("Pronadjeno {Count} termina za 2-satno obaveštenje", twoHourReminders.Count);

                    // Process reminders with timeout
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    cts.CancelAfter(TimeSpan.FromMinutes(4)); // 4-minute timeout for processing

                    // Process 24-hour reminders
                    foreach (var appointment in dailyReminders)
                    {
                        if (appointment.AppointmentDate.HasValue)
                        {
                            try
                            {
                                _logger.LogInformation(
                                    "Procesovanje 24-satnog obaveštenja za termine");
                                await Task.WhenAll(
                                    SendReminderEmail(appointment, "24 sata"),
                                    SendPushNotification(appointment, "24 sata")
                                );

                                var entity = await dbContext.Appointments.FindAsync(appointment.Id);
                                if (entity != null)
                                {
                                    entity.Reminder24HoursSent = true;
                                    await dbContext.SaveChangesAsync(cts.Token);
                                    _logger.LogInformation("Ažuriran status 24-satnog obaveštenja za termine");
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.LogWarning("Procesovanje pauze za 24-satno obaveštenje za termine");
                                break;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Greška prilikom procesovanja 24-satnog obaveštenja za termine");
                            }
                        }
                    }

                    // Process 2-hour reminders
                    foreach (var appointment in twoHourReminders)
                    {
                        try
                        {
                            _logger.LogInformation(
                                "Procesovanje 2-satnog obaveštenja za termine");

                            await Task.WhenAll(
                                SendReminderEmail(appointment, "2 sata"),
                                SendPushNotification(appointment, "2 sata")
                            );

                            var entity = await dbContext.Appointments.FindAsync(appointment.Id);
                            if (entity != null)
                            {
                                entity.Reminder2HoursSent = true;
                                await dbContext.SaveChangesAsync(cts.Token);
                                _logger.LogInformation("Ažuriran status 2-satnog obaveštenja za termine");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogWarning("Procesovanje pauze za 2-satno obaveštenje za termine");
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Greška prilikom procesovanja 2-satnog obaveštenja za termine");
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kritična greška u servisu servisu za obaveštenja");
                // Wait longer on critical errors to prevent rapid retries
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }

            // Wait for 5 minutes before the next check
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }

        _logger.LogInformation("AppointmentReminderService je završio rad");
    }

    private async Task SendReminderEmail(Appointment appointment, string timeFrame)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

            // Get the total duration from all services
            var duration = appointment?.Services != null && appointment.Services.Any() 
                ? appointment.Services.Aggregate(TimeSpan.Zero, (total, service) => total + (service.Duration ?? TimeSpan.Zero))
                : TimeSpan.Zero;

            var appUser = appointment?.ApplicationUser;
            if (string.IsNullOrEmpty(appUser?.Email))
            {
                _logger.LogWarning("Nema emaila za termine");
                return;
            }

            var lang = appUser.PreferredLanguage;
            var durationText = NotificationMessages.FormatAppointmentDuration(lang, duration);
            var (emailSubject, emailBody) = NotificationMessages.AppointmentReminderEmail(
                lang,
                appUser.FirstName,
                appointment!.Business?.Name ?? string.Empty,
                timeFrame,
                durationText);

            await emailService.SendEmailAsync(
                appUser.Email!,
                emailSubject,
                emailBody);

            _logger.LogInformation(
                "Poslat podsetnik email za termine");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Nije uspešno poslat podsetnik email za termine");
        }
    }

    private async Task SendPushNotification(Appointment appointment, string timeFrame)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var lang = appointment.ApplicationUser?.PreferredLanguage;
            var (title, message) = NotificationMessages.AppointmentReminder(
                lang,
                appointment.Business?.Name ?? string.Empty,
                timeFrame);

            await notificationService.SendDirectNotification(
                Guid.Parse(appointment!.ApplicationUserId!),
                title,
                message);

            _logger.LogInformation(
                "Poslata push notifikacija za termine");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Nije uspešno poslata push notifikacija za termine");
        }
    }

    private async Task DebugAppointmentData(AppDbContext dbContext)
    {
        try
        {
            var now = DateTime.UtcNow;
            var serbiaTime = now.AddHours(2); // Serbia is UTC+2
            _logger.LogInformation("Current UTC time: {Now}, Serbia time: {SerbiaTime}", now, serbiaTime);
            
            // Get all appointments for the next 24 hours
            var appointments = await dbContext.Appointments
                .Where(a => a.AppointmentDate.HasValue && 
                           a.AppointmentDate.Value >= now && 
                           a.AppointmentDate.Value <= now.AddHours(24))
                .Select(a => new { 
                    Id = a.Id, 
                    Date = a.AppointmentDate, 
                    StartTime = a.StartTime,
                    Reminder24HoursSent = a.Reminder24HoursSent,
                    Reminder2HoursSent = a.Reminder2HoursSent
                })
                .ToListAsync();
            
            _logger.LogInformation("Found {Count} appointments in the next 24 hours", appointments.Count);
            
            foreach (var apt in appointments)
            {
                var timeUntil = apt.Date?.Subtract(now);
                var serbiaAppointmentTime = apt.Date?.AddHours(2); // Convert to Serbia time
                var hoursUntilAppointment = timeUntil?.TotalHours ?? 0;
                
                _logger.LogInformation(
                    "Appointment {Id}: UTC Date={Date}, Serbia Date={SerbiaDate}, Time={Time}, " +
                    "Hours Until={HoursUntil}, 24hSent={Sent24h}, 2hSent={Sent2h}, " +
                    "Should Send 24h={ShouldSend24h}, Should Send 2h={ShouldSend2h}",
                    apt.Id, 
                    apt.Date?.Date, 
                    serbiaAppointmentTime?.Date,
                    apt.StartTime,
                    hoursUntilAppointment,
                    apt.Reminder24HoursSent,
                    apt.Reminder2HoursSent,
                    hoursUntilAppointment >= 23 && hoursUntilAppointment <= 25,
                    hoursUntilAppointment >= 1.75 && hoursUntilAppointment <= 2
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DebugAppointmentData");
        }
    }
}
