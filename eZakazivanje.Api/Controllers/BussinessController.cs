using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Request;
using eZakazivanje.Entity.DTOS.Responce;
using Microsoft.AspNetCore.Authorization;
using eZakazivanje.Entity.DTOS.Response;
using System.Security.Claims;
using eZakazivanje.DataService.Services;
using eZakazivanje.Api.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using eZakazivanje.DataService.BackgroundServices.Services;

namespace eZakazivanje.Api.Controllers
{
    /// <summary>
    /// Controller for managing business entities, employees, schedules, and related business operations.
    /// Provides endpoints for CRUD operations, employee management, working hours, images, and more.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class BussinessController : BaseController
    {

    private readonly IFileService _fileService;
    private readonly ILogger<BussinessController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EmailService _emailService;
    private readonly BusinessApprovalLinkService _businessApprovalLinkService;


    public BussinessController(IFileService fileService, IUnitOfWork unitOfWork, ILogger<BussinessController> logger, IMapper mapper, UserManager<ApplicationUser> userManager, EmailService emailService, BusinessApprovalLinkService businessApprovalLinkService) : base(unitOfWork, mapper) 
    {
   
        _logger = logger;
        _fileService = fileService;
        _userManager = userManager;
        _emailService = emailService;
        _businessApprovalLinkService = businessApprovalLinkService;
 
    }

    [HttpGet("verify-business")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyBusinessApproval([FromQuery] string token)
    {
        try
        {
            // Handle double-encoded token
            var decodedToken = Uri.UnescapeDataString(Uri.UnescapeDataString(token));

            if (!_businessApprovalLinkService.TryValidateToken(decodedToken, out var businessId, out var userId))
            {
                return BadRequest(new { message = "Invalid or expired token." });
            }

            var business = await _unitOfWork.Bussinesses.GetById(businessId);
            if (business == null)
            {
                return BadRequest(new { message = "Business not found." });
            }

            if (!string.Equals(business.UserId, userId, StringComparison.Ordinal))
            {
                return BadRequest(new { message = "Token does not match business owner." });
            }

            business.ApprovalStatus = BusinessApprovalStatus.Approved;
            business.IsActive = true;
            business.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.CompleteAsync();
            return Ok(new { message = "Business approved successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying business approval token");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while processing your request." });
        }
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBusiness([FromBody] CreateBussiness business)
    {
        _logger.LogInformation("CreateBusiness endpoint called with business name: {BusinessName}", business?.Name ?? "Unknown");
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("CreateBusiness - Invalid model state");
            return BadRequest(ModelState);
        }

        var mappedBusiness = _mapper.Map<Bussiness>(business);

        try
        {
            var result = await _unitOfWork.Bussinesses.CreateBusiness(mappedBusiness);
            if (result)
            {
                await _unitOfWork.CompleteAsync();
                return CreatedAtAction(nameof(GetBusinessById), new { id = mappedBusiness.Id }, _mapper.Map<CreateBussinessResponce>(mappedBusiness));
            }
            else
            {
                return BadRequest("Nije uspelo kreiranje preduzeća. Možda već postoji ili ima nevažećih podatka.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom kreiranja preduzeća");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBusinessById(Guid id)
    {
        var business = await _unitOfWork.Bussinesses.GetById(id);
        
        if (business == null)
        {
            return NotFound();
        }
        return Ok(business);
    }
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteBusiness(Guid id)
    {
        _logger.LogInformation("DeleteBusiness endpoint called for business ID: {BusinessId}", id);
        
        try
        {
            var result = await _unitOfWork.Bussinesses.DeleteBusiness(id);
            if (result)
            {
                await _unitOfWork.CompleteAsync();
                _logger.LogInformation("Business with ID {BusinessId} deleted successfully", id);
                return NoContent();
            }
            else
            {
                _logger.LogWarning("Business with ID {BusinessId} not found for deletion", id);
                return NotFound($"Preduzeće sa ID {id} nije pronađeno.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Došlo je do greške prilikom brisanja preduzeća sa ID {id}");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllBusinesses([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        _logger.LogInformation("GetAllBusinesses endpoint called - Page: {PageNumber}, Size: {PageSize}", pageNumber, pageSize);
        
        try
        {
            var businesses = await _unitOfWork.Bussinesses.GetAllBusinessesPaginated(pageNumber, pageSize);
            var totalCount = await _unitOfWork.Bussinesses.GetTotalBusinessCount();

            var response = new
            {
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Businesses = _mapper.Map<IEnumerable<CreateBussinessResponce>>(businesses)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja preduzeća");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }
    //TODO: Add the employee
    [HttpGet("{businessId}/appointments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBusinessAppointmentsForDate(Guid businessId, [FromQuery] DateTime date)
    {
        try
        {
            // Convert the date to UTC
            var utcDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            
            var result = await _unitOfWork.Bussinesses.GetBusinessAppointmentsForDate(businessId, utcDate);

            if (result == null)
            {
                return NotFound($"Preduzeće sa ID {businessId} nije pronađeno ili nema termina za dati datum.");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom dobijanja termina za preduzeće {BusinessId} na datum {Date}", businessId, date.ToShortDateString());
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    

    [HttpPost("update-services")]
    [Authorize(Roles = UserRoles.Business)] 
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateBusinessServices([FromBody] BusinessUpdateDto updatedBusiness, [FromQuery] Guid? businessId = null)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            var result = await _unitOfWork.Bussinesses.UpdateBusinessServices(targetBusinessId, updatedBusiness);

            if (!result)
            {
                return NotFound($"Preduzeće sa ID {targetBusinessId} nije pronađeno.");
            }

            return Ok($"Preduzeće sa ID {targetBusinessId} je uspešno ažurirano.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom ažuriranja usluga za preduzeće");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet("by-category/{categoryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBusinessesByCategory(Guid categoryId)
    {
        try
        {
            var businesses = await _unitOfWork.Bussinesses.GetBusinessesByCategory(categoryId);
            
            if (!businesses.Any())
            {
                return NotFound($"Nije pronađeno preduzeće za kategoriju ID {categoryId}");
            }

            return Ok(businesses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja preduzeća za kategoriju {CategoryId}", 
                categoryId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet("{id}/details")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBusinessDetails(Guid id)
    {
        try
        {
            var businessDetails = await _unitOfWork.Bussinesses.GetBusinessDetails(id);
            
            if (businessDetails == null)
            {
                return NotFound($"Preduzeće sa ID {id} nije pronađeno.");
            }

            return Ok(businessDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja detalja za preduzeće {BusinessId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPost("employees")]
    [Authorize(Roles = UserRoles.Business)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddEmployeeToBusiness([FromBody] CreateEmployee employeeDto, [FromQuery] Guid? businessId = null)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            var employee = _mapper.Map<Employee>(employeeDto);
            var result = await _unitOfWork.Bussinesses.AddEmployeeToBusiness(targetBusinessId, employee);

            if (!result)
                return BadRequest("Nije uspelo dodavanje zaposlenog u preduzeće");

            await _unitOfWork.CompleteAsync();
            return CreatedAtAction(nameof(GetBusinessById), new { id = targetBusinessId }, employee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom dodavanja zaposlenog u preduzeće");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet("{businessId}/employees")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBusinessEmployees(Guid businessId)
    {
        try
        {
            var employees = await _unitOfWork.Bussinesses.GetBusinessEmployees(businessId);
            
            if (!employees.Any())
                return NotFound($"Nije pronađen zaposleni za preduzeće sa ID {businessId}");

            return Ok(employees);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja zaposlenih za preduzeće {BusinessId}", businessId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet("{businessId}/available-slots")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailableTimeSlots(
        Guid businessId,
        [FromQuery] Guid employeeId,
        [FromQuery] Guid serviceId,
        [FromQuery] DateTime date)
    {
        try
        {
            // Convert the date to UTC
            var utcDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            
            var slots = await _unitOfWork.Bussinesses.GetAvailableTimeSlots(businessId, employeeId, serviceId, utcDate);
            if (slots == null || !slots.AvailableTimeSlots.Any())
            {
                return NotFound("Nema dostupnih termina za izabrani datum.");
            }

            return Ok(slots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom dobijanja dostupnih termina");
            return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPut("working-hours")]
    [Authorize(Roles = UserRoles.Business)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBusinessHours([FromBody] UpdateBusinessHoursRequest request, [FromQuery] Guid? businessId = null)
    {
        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            var result = await _unitOfWork.Bussinesses.UpdateBusinessHours(
                targetBusinessId, 
                request.StartWorkHours, 
                request.EndWorkHours,
                request.Tick);

            if (!result)
            {
                return NotFound("Preduzeće nije pronađeno ili su pogrešni radni sati.");
            }

            await _unitOfWork.CompleteAsync();
            return Ok("Radni sati preduzeća su uspešno ažurirani");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom ažuriranja radnih sati preduzeća");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPost("update-images")]
    [Authorize(Roles = UserRoles.Business)]
    [ApprovedBusiness]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB max request size
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateBusinessImages([FromForm] List<IFormFile> imageFiles, [FromQuery] Guid? businessId = null)
    {
        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            var imagePaths = new List<string>();

            if (imageFiles != null && imageFiles.Any())
            {
                foreach (var imageFile in imageFiles)
                {
                    if (!_fileService.IsValidImage(imageFile))
                    {
                        return BadRequest($"Nevalidan fajl slike: {imageFile.FileName}. Samo .jpg, .jpeg, i .png fajlovi do 1MB su dozvoljeni.");
                    }

                    var imagePath = await _fileService.UploadImageAsync(imageFile, "business", targetBusinessId);
                    imagePaths.Add(imagePath);
                }
            }

            var result = await _unitOfWork.Bussinesses.UpdateBusinessImagesAsync(targetBusinessId, imagePaths);
            if (result == null)
            {
                return NotFound($"Preduzeće sa ID {targetBusinessId} nije pronađeno");
            }

            await _unitOfWork.CompleteAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom ažuriranja slika za preduzeće");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPatch("{businessId}/toggle-active")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleBusinessActive(Guid businessId, [FromBody] bool isActive)
    {
        try
        {
            var result = await _unitOfWork.Bussinesses.ToggleBusinessActiveStatusAsync(businessId, isActive);
            
            if (!result)
            {
                return NotFound($"Preduzeće sa ID {businessId} nije pronađeno");
            }

            return Ok(new { Message = $"Status aktivnosti preduzeća je uspešno ažuriran na {isActive}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom ažuriranja statusa aktivnosti preduzeća");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPost("free-time")]
    [Authorize(Roles = UserRoles.Business)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddFreeTimeSlot([FromBody] CreateFreeTimeSlotRequest request, [FromQuery] Guid? businessId = null)
    {
        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            var result = await _unitOfWork.Bussinesses.AddFreeTimeSlotAsync(
                targetBusinessId,
                request.EmployeeId,
                request.Date,
                request.StartTime,
                request.EndTime,
                request.Note,
                request.ServiceName
            );

            if (!result)
            {
                return BadRequest("Nevalidan termin ili konflikt rasporeda");
            }

            await _unitOfWork.CompleteAsync();
            return Ok("Dostupan termin je uspešno dodan");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom dodavanja dostupnog termina");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPost("free-time-range")]
    [Authorize(Roles = UserRoles.Business)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddFreeTimeSlotRange([FromBody] CreateFreeTimeSlotRangeRequest request, [FromQuery] Guid? businessId = null)
    {
        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            if (request.StartDate > request.EndDate)
            {
                return BadRequest("Početni datum mora biti pre krajnjeg datuma");
            }

            var result = await _unitOfWork.Bussinesses.AddFreeTimeSlotRangeAsync(
                targetBusinessId,
                request.EmployeeId,
                request.StartDate,
                request.EndDate,
                request.StartTime,
                request.EndTime,
                request.Note,
                request.ServiceName
            );

            if (!result)
            {
                return BadRequest("Nevalidan termin ili konflikt rasporeda");
            }

            await _unitOfWork.CompleteAsync();
            return Ok("Dostupni termini su uspešno dodani za navedeni period");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom dodavanja dostupnih termina za period");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpDelete("free-time/{freeTimeSlotId}")]
    [Authorize(Roles = UserRoles.Business)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFreeTimeSlot(Guid freeTimeSlotId, [FromQuery] Guid? businessId = null)
    {
        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            var result = await _unitOfWork.Bussinesses.DeleteFreeTimeSlotAsync(
                targetBusinessId,
                freeTimeSlotId
            );

            if (!result)
            {
                return NotFound("Slobodan vremenski slot nije pronađen ili ne pripada vašem biznisu");
            }

            await _unitOfWork.CompleteAsync();
            return Ok("Slobodan vremenski slot je uspešno obrisan");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom brisanja slobodnog vremenskog slota");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpDelete("free-time-range")]
    [Authorize(Roles = UserRoles.Business)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteFreeTimeSlotRange([FromBody] DeleteFreeTimeSlotRangeRequest request, [FromQuery] Guid? businessId = null)
    {
        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            if (request.StartDate > request.EndDate)
            {
                return BadRequest("Početni datum mora biti pre krajnjeg datuma");
            }

            var result = await _unitOfWork.Bussinesses.DeleteFreeTimeSlotRangeAsync(
                targetBusinessId,
                request.EmployeeId,
                request.StartDate,
                request.EndDate
            );

            if (!result)
            {
                return BadRequest("Došlo je do greške prilikom brisanja slobodnih termina");
            }

            await _unitOfWork.CompleteAsync();
            return Ok("Slobodni termini su uspešno obrisani za navedeni period");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom brisanja slobodnih termina za period");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet("{businessId}/free-time-slots")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFreeTimeSlots(
        Guid businessId,
        [FromQuery] Guid employeeId,
        [FromQuery] DateTime date)
    {
        try
        {
            _logger.LogInformation("Zahtev za dostupne termine za preduzeće");

            var freeSlots = await _unitOfWork.Bussinesses.GetFreeTimeSlotsForDate(
                businessId, employeeId, date);

            if (!freeSlots.Any())
            {
                _logger.LogWarning(
                    "Nije pronađen dostupan termin za preduzeće");
                return NotFound(new { 
                    message = "Nije pronađen dostupan termin za specificirane kriterijume.",
                    requestedDate = date,
                    businessId = businessId,
                    employeeId = employeeId
                });
            }

            return Ok(new {
                message = $"Pronađeno {freeSlots.Count()} dostupnih termina",
                slots = freeSlots
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja dostupnih termina");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet("{businessId}/free-time-slots-range")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFreeTimeSlotsForRange(
        Guid businessId,
        [FromQuery] Guid employeeId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            if (startDate > endDate)
            {
                return BadRequest("Početni datum mora biti pre krajnjeg datuma");
            }

            _logger.LogInformation("Zahtev za dostupne termine za preduzeće");

            var freeSlots = await _unitOfWork.Bussinesses.GetFreeTimeSlotsForDateRange(
                businessId, employeeId, startDate, endDate);

            if (!freeSlots.Any())
            {
                _logger.LogWarning(
                    "Nije pronađen dostupan termin za preduzeće");
                return NotFound(new { 
                    message = "Nije pronađen dostupan termin za specificirane kriterijume.",
                    startDate = startDate,
                    endDate = endDate,
                    businessId = businessId,
                    employeeId = employeeId
                });
            }

            return Ok(new {
                message = $"Pronađeno {freeSlots.Count()} dostupnih termina",
                slots = freeSlots
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja dostupnih termina za period");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

/*    
    [HttpGet("images")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBusinessImage([FromQuery] string imagePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return BadRequest("Image path is required");
            }

            var imageResult = await _fileService.GetImageAsync(imagePath);
            if (imageResult == null)
            {
                return NotFound("Image not found");
            }

            var (fileContents, contentType) = imageResult.Value;
            return File(fileContents, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while retrieving image {ImagePath}", imagePath);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "An error occurred while processing your request.");
        }
    }
    */
    

    [HttpDelete("images")]
    [Authorize(Roles = UserRoles.Business)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBusinessImage([FromQuery] string imagePath, [FromQuery] Guid businessId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return BadRequest("Putanja slike je obavezna");
            }

            if (businessId == Guid.Empty)
            {
                return BadRequest("businessId je obavezan");
            }

            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            // First remove the image from the business record
            var result = await _unitOfWork.Bussinesses.DeleteBusinessImageAsync(targetBusinessId, imagePath);
            if (!result)
            {
                return NotFound("Slika nije pronađena ili ne pripada ovom preduzeću");
            }

            // Then delete the physical file
            var fileDeleted = await _fileService.DeleteImageAsync(imagePath);
            if (!fileDeleted)
            {
                _logger.LogWarning("Slika ne moze biti obrisana: {ImagePath}", imagePath);
            }

            await _unitOfWork.CompleteAsync();
            return Ok("Slika je uspešno obrisana");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom brisanja slike preduzeća");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPost("address")]
    [Authorize(Roles = UserRoles.Business)]
    [ApprovedBusiness]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddBusinessAddress([FromBody] CreateBusinessAddressDto addressDto, [FromQuery] Guid? businessId = null)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Nije pronađen korisnički ID u tokenu");
            }

            var address = _mapper.Map<Address>(addressDto);
            address.ApplicationUserId = userId;

            var result = await _unitOfWork.Bussinesses.AddBusinessAddress(
                targetBusinessId, 
                address,
                addressDto.PIB,
                addressDto.PhoneNumber);

            if (!result)
                return BadRequest("Nije uspelo dodavanje adrese preduzeću");

            await _unitOfWork.CompleteAsync();
            return CreatedAtAction(nameof(GetBusinessById), new { id = targetBusinessId }, address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom dodavanja adrese preduzeću");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPut("contact")]
    [Authorize(Roles = UserRoles.Business)]
    [ApprovedBusiness]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateBusinessContact([FromBody] UpdateBusinessContactDto contactDto, [FromQuery] Guid? businessId = null)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            var result = await _unitOfWork.Bussinesses.UpdateBusinessContactInfo(
                targetBusinessId, 
                contactDto.PIB, 
                contactDto.PhoneNumber
            );

            if (!result)
                return BadRequest("Nije uspelo ažuriranje informacija o kontaktu preduzeća");

            await _unitOfWork.CompleteAsync();
            return Ok("Informacije o kontaktu preduzeća su uspešno ažurirane");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom ažuriranja informacija o kontaktu preduzeća");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet("nearby")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<NearbyBusinessResponse>>> GetNearbyBusinesses(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] int limit = 5)
    {
        try
        {
            if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            {
                return BadRequest("Neispravne koordinate");
            }

            var businesses = await _unitOfWork.Bussinesses.GetNearbyBusinesses(latitude, longitude, limit);
            return Ok(businesses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja preduzeća koja su blizu");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet("my-categories-services")]
    [Authorize(Roles = UserRoles.Business)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<CategoryWithServices>>> GetMyBusinessCategoriesWithServices([FromQuery] Guid? businessId = null)
    {
        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            var result = await _unitOfWork.Bussinesses
                .GetBusinessCategoriesWithServices(targetBusinessId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja kategorija i usluga preduzeća");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPost("schedule")]
    [Authorize(Roles = UserRoles.Business)]
    public async Task<IActionResult> SetBusinessSchedule([FromBody] BusinessScheduleRequest request, [FromQuery] Guid? businessId = null)
    {
        var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
        if (!success)
        {
            return Forbid(errorMessage ?? "Access denied");
        }

        var result = await _unitOfWork.Bussinesses.SetBusinessSchedule(
            targetBusinessId, 
            request
        );

        return result ? Ok() : BadRequest();
    }

    [HttpGet("{businessId}/schedule")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBusinessSchedule(
        Guid businessId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            // Convert dates to UTC
            var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

            var schedule = await _unitOfWork.Bussinesses.GetBusinessSchedule(businessId, utcStartDate, utcEndDate);
            if (schedule == null || !schedule.Any())
            {
                return NotFound("Nije pronađen raspored za specificirane datume.");
            }

            return Ok(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom dobijanja rasporeda za biznis");
            return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPost("schedule-range")]
    [Authorize(Roles = UserRoles.Business)]
    [ApprovedBusiness]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetBusinessScheduleRange([FromBody] BusinessScheduleRangeRequest request, [FromQuery] Guid? businessId = null)
    {
        
        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            if (request.StartDate > request.EndDate)
            {
                return BadRequest("Pocetni datum mora biti pre krajnjeg datuma");
            }

            var result = await _unitOfWork.Bussinesses.SetBusinessScheduleRange(
                targetBusinessId, 
                request
            );

            return result ? Ok() : BadRequest();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom postavljanja rasporeda preduzeća");
            return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPost("weekly-schedule-range")]
    [Authorize(Roles = UserRoles.Business)]
    [ApprovedBusiness]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetBusinessWeeklyTemplateRange([FromBody] SetBusinessWeeklyTemplateRangeRequest request, [FromQuery] Guid? businessId = null)
    {
        var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
        if (!success)
        {
            return Forbid(errorMessage ?? "Access denied");
        }
        
        var result = await _unitOfWork.Bussinesses.SetBusinessWeeklyTemplateRange(targetBusinessId, request);
        if (result)
            return Ok("Business weekly template schedule updated.");
        return StatusCode(500, "Failed to update schedule.");
    }

    [HttpDelete("schedule-range")]
    [Authorize(Roles = UserRoles.Business)]
    [ApprovedBusiness]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteBusinessScheduleRange([FromBody] DeleteBusinessScheduleRangeRequest request, [FromQuery] Guid? businessId = null)
    {
        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage ?? "Access denied");
            }

            if (request.StartDate > request.EndDate)
            {
                return BadRequest("Pocetni datum mora biti pre krajnjeg datuma");
            }

            var result = await _unitOfWork.Bussinesses.DeleteBusinessScheduleRange(
                targetBusinessId, 
                request.StartDate, 
                request.EndDate
            );

            return result ? Ok() : BadRequest();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom brisanja rasporeda preduzeća");
            return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet("{businessId}/availability")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> IsBusinessAvailable(Guid businessId, [FromQuery] DateTime date)
    {
        try
        {
            var result = await _unitOfWork.Bussinesses.IsBusinessAvailableOnDate(businessId, date);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom proveravanja dostupnosti preduzeća");
            return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet("by-category/{categoryId}/city")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBusinessesByCategoryAndCity(Guid categoryId, [FromQuery] string city)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return BadRequest("Parametar grad je obavezan");
            }

            var businesses = await _unitOfWork.Bussinesses.GetBusinessesByCategoryAndCity(categoryId, city);
            
            if (!businesses.Any())
            {
                return NotFound($"Nije pronađeno preduzeće za zadatu kategoriju u gradu {city}");
            }

            return Ok(businesses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja preduzeća za zadatu kategoriju u gradu {City}", city);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    /// <summary>
    /// Get all businesses owned by the current authenticated user
    /// </summary>
    [HttpGet("my-businesses")]
    [Authorize(Roles = UserRoles.Business)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyBusinesses()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var businesses = await _unitOfWork.Context.Businesses
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var response = businesses.Select(b => new
            {
                Id = b.Id,
                Name = b.Name,
                Description = b.Description,
                IsActive = b.IsActive,
                ApprovalStatus = b.ApprovalStatus.ToString(),
                CreatedAt = b.CreatedAt,
                ImageUrls = b.ImageUrls
            }).ToList();

            _logger.LogInformation("Retrieved {Count} businesses for user {UserId}", businesses.Count, userId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving businesses for user");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    /// <summary>
    /// Create an additional business for an existing business user
    /// </summary>
    [HttpPost("create")]
    [Authorize(Roles = UserRoles.Business)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAdditionalBusiness([FromBody] CreateAdditionalBusinessRequest? businessDto)
    {
        try
        {
            if (businessDto == null)
            {
                _logger.LogWarning("CreateAdditionalBusiness - Request body is null or invalid JSON");
                return BadRequest(new { message = "Request body is required and must be valid JSON" });
            }

            // Log incoming request data
            _logger.LogInformation("CreateAdditionalBusiness - Received request. Name: {Name}, Description: {Description}", 
                businessDto.Name ?? "null", businessDto.Description ?? "null");

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .Select(x => new { Field = x.Key, Errors = x.Value?.Errors.Select(e => e.ErrorMessage) })
                    .ToList();
                
                _logger.LogWarning("CreateAdditionalBusiness - Model validation failed. Errors: {Errors}", 
                    System.Text.Json.JsonSerializer.Serialize(errors));
                
                // Return a more detailed error response
                var errorResponse = new
                {
                    message = "Validation failed",
                    errors = errors
                };
                
                return BadRequest(errorResponse);
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID not found in token");
            }

            // Get user information for approval email
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized("User not found");
            }

            // Create new business with new GUID and link to user
            // All businesses (main and additional) require approval - same as main business
            var business = new Bussiness
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = businessDto.Name,
                Description = businessDto.Description,
                IsActive = true,
                ApprovalStatus = BusinessApprovalStatus.Approved,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _unitOfWork.Bussinesses.CreateBusiness(business);
            if (result)
            {
                await _unitOfWork.CompleteAsync();
                
                _logger.LogInformation("Additional business created: BusinessId={BusinessId}, UserId={UserId}, ApprovalStatus={ApprovalStatus}, IsActive={IsActive}.", 
                    business.Id, userId, business.ApprovalStatus, business.IsActive);
                return CreatedAtAction(nameof(GetBusinessById), new { id = business.Id }, 
                    _mapper.Map<CreateBussinessResponce>(business));
            }
            else
            {
                return BadRequest("Nije uspelo kreiranje preduzeća. Možda već postoji ili ima nevažećih podatka.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating additional business for user");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    }
}
