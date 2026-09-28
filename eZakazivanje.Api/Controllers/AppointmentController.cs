using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Request;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using eZakazivanje.Entity.DTOS.Response;
using eZakazivanje.Api.Authorization;
using eZakazivanje.DataService.Services;

namespace eZakazivanje.Api.Controllers
{
    /// <summary>
    /// Controller for managing appointments
    /// Provides CRUD operations and appointment booking functionality
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentController : BaseController
    {
        private readonly ILogger<AppointmentController> _logger;
        private readonly INotificationService _notificationService;

        /// <summary>
        /// Constructor for AppointmentController
        /// </summary>
        /// <param name="unitOfWork">Unit of work for data access</param>
        /// <param name="logger">Logger for error tracking</param>
        /// <param name="mapper">AutoMapper for object mapping</param>
        /// <param name="notificationService">Notification service for Firebase notifications</param>
        public AppointmentController(
            IUnitOfWork unitOfWork, 
            ILogger<AppointmentController> logger, 
            IMapper mapper,
            INotificationService notificationService): base(unitOfWork, mapper)
        {
            _logger = logger;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Retrieves all appointments in the system
        /// </summary>
        /// <returns>
        /// 200 OK: List of all appointments
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Appointment>>> GetAllAppointments()
        {
            try
            {
                // Retrieve all appointments from the database
                var appointments = await _unitOfWork.Appointments.GetAllAppointments();
                return Ok(_mapper.Map<IEnumerable<Appointment>>(appointments));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom dobijanja svih termina");
                return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        /// <summary>
        /// Retrieves a specific appointment by its ID
        /// </summary>
        /// <param name="id">The unique identifier of the appointment</param>
        /// <returns>
        /// 200 OK: Appointment found and returned
        /// 404 Not Found: Appointment with specified ID not found
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Appointment>> GetAppointmentById(Guid id)
        {
            try
            {
                // Retrieve appointment by ID from the database
                var appointment = await _unitOfWork.Appointments.GetAppointmentById(id);
                
                // Return 404 if appointment is not found
                if (appointment == null)
                {
                    return NotFound($"Termin nije pronađen");
                }

                return Ok(appointment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom dobijanja termina");
                return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        /// <summary>
        /// Creates a new appointment (Note: This method has a bug - always returns 500)
        /// </summary>
        /// <param name="appointment">Appointment data to create</param>
        /// <returns>
        /// 201 Created: Appointment successfully created
        /// 400 Bad Request: Invalid model state
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Appointment>> CreateAppointment([FromBody] CreateAppointment appointment)
        {
            try
            {
                // Validate the incoming model
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Map DTO to entity and create appointment
                var newAppointment = _mapper.Map<Appointment>(appointment);
                var createdAppointment = await _unitOfWork.Appointments.CreateAppointment(newAppointment);
                
                // Check if appointment creation was successful
                if (createdAppointment == null)
                {
                    return StatusCode(500, "Nije uspelo kreiranje termina");
                }

                // BUG: This should return the created appointment, not an error
                return StatusCode(500, "Došlo je do greške prilikom kreiranja termina");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom kreiranja termina");
                return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        /// <summary>
        /// Deletes an appointment by its ID
        /// </summary>
        /// <param name="id">The unique identifier of the appointment to delete</param>
        /// <returns>
        /// 204 No Content: Appointment successfully deleted
        /// 404 Not Found: Appointment with specified ID not found
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteAppointment(Guid id)
        {
            try
            {
                // Attempt to delete the appointment
                var success = await _unitOfWork.Appointments.DeleteAppointment(id);
                
                // Return 404 if appointment was not found or deletion failed
                if (!success)
                {
                    return NotFound($"Termin nije pronađen");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom brisanja termina");
                return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        /// <summary>
        /// Deletes an appointment by business and sends notification to the user
        /// </summary>
        /// <param name="id">The unique identifier of the appointment</param>
        /// <returns>
        /// 204 No Content: Appointment successfully deleted
        /// 404 Not Found: Appointment with specified ID not found
        /// 403 Forbidden: Business not authorized to delete this appointment
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpDelete("business/{id}")]
        [Authorize(Roles = UserRoles.Business)]
        [ApprovedBusiness]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteAppointmentByBusiness(Guid id, [FromQuery] Guid? businessId = null)
        {
            try
            {
                // Get and validate business ID from query parameter or token
                var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
                if (!success)
                {
                    return Forbid(errorMessage ?? "Access denied");
                }

                // Delete appointment using repository method with business validation
                var (deleteSuccess, userId, appointmentDetails) = await _unitOfWork.Appointments.DeleteAppointmentByBusiness(id, targetBusinessId);
                
                if (!deleteSuccess)
                {
                    return NotFound($"Termin nije pronađen ili nemate dozvolu da ga brišete");
                }

                // Send notification to the user about appointment cancellation
                if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out Guid userGuid) && appointmentDetails != null)
                {
                    try
                    {
                        // Get business name from business ID
                        var business = await _unitOfWork.Bussinesses.GetById(targetBusinessId);
                        var businessName = business?.Name ?? "Business";

                        var customer = await _unitOfWork.Context.Users.AsNoTracking()
                            .FirstOrDefaultAsync(u => u.Id == userId);
                        var date = appointmentDetails.AppointmentDate ?? DateTime.UtcNow.Date;
                        var time = appointmentDetails.StartTime ?? TimeSpan.Zero;
                        var (cancelTitle, cancelBody) = NotificationMessages.AppointmentCancelledForCustomer(
                            customer?.PreferredLanguage,
                            businessName,
                            date,
                            time);

                        await _notificationService.SendDirectNotification(
                            userGuid,
                            cancelTitle,
                            cancelBody);
                        _logger.LogInformation("Cancellation notification sent to user for appointment");
                    }
                    catch (Exception notificationEx)
                    {
                        _logger.LogError(notificationEx, "Failed to send cancellation notification for appointment");
                        // Don't fail the deletion if notification fails
                    }
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom brisanja termina od strane biznisa");
                return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        /// <summary>
        /// Books a new appointment for an authenticated user
        /// Includes business validation, working hours check, and conflict detection
        /// </summary>
        /// <param name="request">Appointment booking request with business, employee, and service details</param>
        /// <returns>
        /// 201 Created: Appointment successfully booked
        /// 400 Bad Request: Invalid request data or business validation failed
        /// 401 Unauthorized: User not authenticated
        /// 403 Forbidden: User doesn't have required role
        /// 409 Conflict: Requested time slot is not available (double booking)
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpPost("book")]
        [Authorize(Roles = $"{UserRoles.User},{UserRoles.Admin}")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<AppointmentBookingResponse>> CreateAppointment([FromBody] CreateAppointmentRequest request)
        {
            try
            {
                // Extract and validate user ID from JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out Guid userId))
                {
                    return BadRequest("Nevalidan identifikator korisnika");
                }

                // Verify that the business exists
                var business = await _unitOfWork.Bussinesses.GetById(request.BusinessId);
                if (business == null)
                {
                    return BadRequest("Posao nije pronađen");
                }

                // Check if business is approved and active
                if (business.ApprovalStatus != BusinessApprovalStatus.Approved || !business.IsActive.GetValueOrDefault())
                {
                    return BadRequest("Ovaj biznis još nije odobren ili nije aktivan");
                }

                // Get business schedule for the specific date
                var schedule = await _unitOfWork.Context.BusinessSchedules
                    .FirstOrDefaultAsync(s => s.BusinessId == request.BusinessId && s.Date.Date == request.AppointmentDate.Date);

                // Validate that appointment time is within custom schedule only
                var workStartTime = schedule?.CustomStartTime ?? TimeSpan.Zero;
                var workEndTime = schedule?.CustomEndTime ?? TimeSpan.FromHours(24);

                if (workStartTime > request.StartTime || workEndTime < request.StartTime)
                {
                    return BadRequest("Nevalidni radni sati");
                }

                // Attempt to create the appointment
                var response = await _unitOfWork.Appointments.CreateUserAppointment(userId, request);
                if (response == null)
                {
                    // Check for double-booking conflicts by examining existing appointments
                    var conflictingAppointment = await _unitOfWork.Appointments.GetAllAppointments();
                    var hasConflict = conflictingAppointment.Any(a => 
                        a.EmployeeId == request.EmployeeId && 
                        a.AppointmentDate == request.AppointmentDate &&
                        a.IsCancelled != true);
                    
                    if (hasConflict)
                    {
                        return Conflict("Traženi termin nije dostupan. Molimo odaberite drugi termin.");
                    }
                    
                    return BadRequest("Nije uspelo kreiranje termina");
                }

                // Save changes to database
                await _unitOfWork.CompleteAsync();

                // Send Firebase notification to business
                try
                {
                    // Get service name for notification
                    var service = await _unitOfWork.Services.GetById(request.ServiceId);
                    var serviceName = service?.Name ?? "Service";

                    var bookingUser = await _unitOfWork.Context.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == userId.ToString());
                    var userFullName = bookingUser != null
                        ? $"{bookingUser.FirstName} {bookingUser.LastName}".Trim()
                        : string.Empty;
                    if (string.IsNullOrEmpty(userFullName))
                        userFullName = "User";

                    var owner = await _unitOfWork.Context.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == business.UserId);
                    var (bookTitle, bookBody) = NotificationMessages.AppointmentBookedForBusiness(
                        owner?.PreferredLanguage,
                        serviceName,
                        userFullName,
                        request.AppointmentDate,
                        request.StartTime);

                    await _notificationService.SendNotificationToBusiness(
                        business.Id,
                        bookTitle,
                        bookBody);

                    _logger.LogInformation("Firebase notification sent to business for appointment");
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(notificationEx, "Failed to send Firebase notification for appointment. Error: {Error}", notificationEx.Message);
                    // Don't fail the appointment creation if notification fails
                }

                return CreatedAtAction(nameof(GetAppointmentById), new { id = response.AppointmentId }, response);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException?.Message?.Contains("duplicate key") == true)
            {
                // Handle database constraint violations (double booking)
                _logger.LogWarning(ex, "Pokušaj duplog rezervisanja termina");
                return Conflict("Traženi termin nije dostupan. Molimo odaberite drugi termin.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom kreiranja termina");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        /// <summary>
        /// Retrieves upcoming appointments for the authenticated user
        /// </summary>
        /// <returns>
        /// 200 OK: List of upcoming appointments
        /// 401 Unauthorized: User not authenticated
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpGet("upcoming")]
        [Authorize(Roles = "User")]
        public async Task<ActionResult<IEnumerable<AppointmentResponse>>> GetUpcomingAppointments()
        {
            try
            {
                // Extract user ID from JWT token
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                // Get upcoming appointments for the user
                var appointments = await _unitOfWork.Appointments.GetUpcomingAppointmentsForUserAsync(userId);
                
                // Map appointments to response DTOs with business and employee details
                var response = appointments.Select(a => new AppointmentResponse
                {
                    Id = a.Appointment?.Id ?? Guid.Empty,
                    BusinessName = a.Appointment?.Business?.Name,
                    EmployeeId = a.Appointment?.EmployeeId ?? Guid.Empty,
                    EmployeeName = $"{a.Appointment?.Employee?.FirstName} {a.Appointment?.Employee?.LastName}",
                    AppointmentDate = a.Appointment?.AppointmentDate,
                    StartTime = a.Appointment?.StartTime,
                    EndTime = a.Appointment?.EndTime,
                    TotalPrice = a.Appointment?.TotalPrice,
                    Services = a.Appointment?.Services?.Select(s => new ServiceDto
                    {
                        Name = s.Name,
                        Price = s.Price,
                        Currency = string.IsNullOrWhiteSpace(s.Currency) ? null : s.Currency,
                        Duration = s.Duration
                    }).ToList()
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom dobijanja buducih termina");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        /// <summary>
        /// Retrieves past appointments for the authenticated user
        /// </summary>
        /// <returns>
        /// 200 OK: List of past appointments
        /// 401 Unauthorized: User not authenticated
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpGet("past")]
        [Authorize(Roles = "User")]
        public async Task<ActionResult<IEnumerable<AppointmentResponse>>> GetPastAppointments()
        {
            try
            {
                // Extract user ID from JWT token
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                // Get past appointments for the user
                var appointments = await _unitOfWork.Appointments.GetPastAppointmentsForUserAsync(userId);
                
                // Map appointments to response DTOs with business and employee details
                var response = appointments.Select(a => new AppointmentResponse
                {
                    Id = a.Appointment?.Id ?? Guid.Empty,
                    BusinessName = a.Appointment?.Business?.Name,
                    EmployeeId = a.Appointment?.EmployeeId ?? Guid.Empty,
                    EmployeeName = $"{a.Appointment?.Employee?.FirstName} {a.Appointment?.Employee?.LastName}",
                    AppointmentDate = a.Appointment?.AppointmentDate,
                    StartTime = a.Appointment?.StartTime,
                    EndTime = a.Appointment?.EndTime,
                    TotalPrice = a.Appointment?.TotalPrice,
                    Services = a.Appointment?.Services?.Select(s => new ServiceDto
                    {
                        Name = s.Name,
                        Price = s.Price,
                        Currency = string.IsNullOrWhiteSpace(s.Currency) ? null : s.Currency,
                        Duration = s.Duration
                    }).ToList()
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom dobijanja zavrsenih termina");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        /// <summary>
        /// Creates a related appointment (e.g., follow-up appointment)
        /// </summary>
        /// <param name="request">Request containing details for the related appointment</param>
        /// <returns>
        /// 201 Created: Related appointment successfully created
        /// 400 Bad Request: Invalid request data
        /// 401 Unauthorized: User not authenticated
        /// 409 Conflict: Requested time slot is not available
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpPost("create-related")]
        [Authorize(Roles = $"{UserRoles.User},{UserRoles.Admin}")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AppointmentBookingResponse>> CreateRelatedAppointment(
            [FromBody] CreateRelatedAppointmentRequest request)
        {
            try
            {
                // Extract and validate user ID from JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out Guid userId))
                {
                    return BadRequest("Nevalidan identifikator korisnika");
                }

                // Create the related appointment
                var response = await _unitOfWork.Appointments.CreateRelatedAppointment(userId, request);
                if (response == null)
                {
                    return BadRequest("Nije uspelo kreiranje povezanog termina");
                }

                // Save changes to database and return success response
                await _unitOfWork.CompleteAsync();
                return CreatedAtAction(nameof(GetAppointmentById), new { id = response.AppointmentId }, response);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException?.Message?.Contains("duplicate key") == true)
            {
                // Handle database constraint violations (double booking)
                _logger.LogWarning(ex, "Pokušaj duplog rezervisanja povezanog termina");
                return Conflict("Traženi termin nije dostupan. Molimo odaberite drugi termin.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom kreiranja povezanog termina");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        /// <summary>
        /// Retrieves an appointment with its related appointments
        /// </summary>
        /// <param name="id">The unique identifier of the appointment</param>
        /// <returns>
        /// 200 OK: Appointment with related appointments found and returned
        /// 404 Not Found: Appointment with specified ID not found
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpGet("{id}/with-related")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AppointmentWithRelatedResponse>> GetAppointmentWithRelated(Guid id)
        {
            try
            {
                // Retrieve appointment with related appointments from database
                var appointment = await _unitOfWork.Appointments.GetAppointmentWithRelated(id);
                
                // Return 404 if appointment is not found
                if (appointment == null)
                {
                    return NotFound($"Termin nije pronađen");
                }

                return Ok(appointment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom dobijanja termina sa povezanim terminima");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }
    }
}
