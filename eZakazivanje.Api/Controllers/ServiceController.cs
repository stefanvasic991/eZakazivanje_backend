using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Request;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace eZakazivanje.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceController : BaseController
    {
        private readonly ILogger<ServiceController> _logger;

        public ServiceController(IUnitOfWork unitOfWork, IMapper mapper, ILogger<ServiceController> logger) : base(unitOfWork, mapper)
        {
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllServices()
        {
            try
            {
                var services = await _unitOfWork.Services.GetAllServices();
                return Ok(services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja svih usluga");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva");
            }
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetServiceById(Guid id)
        {
            try
            {
                var service = await _unitOfWork.Services.GetServiceById(id);
                
                if (service == null)
                {
                    return NotFound($"Usluga sa ID {id} nije pronađena");
                }

                return Ok(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja usluge {ServiceId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva");
            }
        }

        [HttpPost]
        [Authorize(Roles = UserRoles.Business)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateService(
            [FromBody] CreateService serviceDto,
            [FromQuery] Guid? businessId = null,
            [FromQuery] Guid? categoryId = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!TimeSpan.TryParse(serviceDto.Duration, out var parsedDuration))
            {
                return BadRequest("Nevalidan format trajanja. Molim vas koristite format 'HH:mm:ss' ili 'HH:mm'");
            }

            try
            {
                var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
                if (!success)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, errorMessage ?? "Nemate pristup traženom biznisu.");
                }

                var service = _mapper.Map<Service>(serviceDto);
                service.Duration = parsedDuration;
                service.BusinessId = targetBusinessId;

                if (!string.IsNullOrWhiteSpace(serviceDto.Currency))
                {
                    var currency = serviceDto.Currency.Trim().ToUpperInvariant();
                    if (currency.Length != 3)
                    {
                        return BadRequest("Currency mora biti ISO-4217 kod od 3 slova (npr. RSD, EUR, USD).");
                    }

                    service.Currency = currency;
                }

                if (categoryId.HasValue)
                {
                    var categoryExists = await _unitOfWork.Context.Categories.AnyAsync(c => c.Id == categoryId.Value);
                    if (!categoryExists)
                    {
                        return BadRequest("Kategorija nije pronađena");
                    }

                    // Ensure this category is linked to the business (business may have limited categories)
                    var categoryIsLinkedToBusiness = await _unitOfWork.Context.Businesses
                        .Where(b => b.Id == targetBusinessId)
                        .SelectMany(b => b.Categories!)
                        .AnyAsync(c => c.Id == categoryId.Value);

                    if (!categoryIsLinkedToBusiness)
                    {
                        return BadRequest("Kategorija nije povezana sa odabranim biznisom");
                    }

                    service.CategoryId = categoryId.Value;
                }

                var createdService = await _unitOfWork.Services.CreateService(service);

                if (createdService == null)
                {
                    return BadRequest("Nije uspelo kreiranje usluge");
                }

                return CreatedAtAction(nameof(GetServiceById), 
                    new { id = createdService.Id }, createdService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom kreiranja usluge");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva");
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateService(Guid id, [FromBody] CreateService serviceDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var service = _mapper.Map<Service>(serviceDto);
                var result = await _unitOfWork.Services.UpdateService(id, service);

                if (!result)
                {
                    return NotFound($"Usluga sa ID {id} nije pronađena");
                }

                return Ok("Usluga je uspešno ažurirana");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom ažuriranja usluge {ServiceId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva");
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteService(Guid id)
        {
            try
            {
                var result = await _unitOfWork.Services.DeleteService(id);

                if (!result)
                {
                    return NotFound($"Usluga sa ID {id} nije pronađena");
                }

                return Ok("Usluga je uspešno obrisana");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom brisanja usluge {ServiceId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva");
            }
        }

        [HttpPut("{serviceId}/category/{categoryId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddServiceToCategory(Guid serviceId, Guid categoryId)
        {
            try
            {
                var result = await _unitOfWork.Services.AddServiceToCategory(serviceId, categoryId);

                if (!result)
                {
                    return NotFound("Usluga ili kategorija nije pronađena");
                }

                await _unitOfWork.CompleteAsync();
                return Ok($"Usluga {serviceId} je uspešno dodata kategoriji {categoryId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Došlo je do greške prilikom dodavanja usluge {ServiceId} u kategoriju {CategoryId}", 
                    serviceId, 
                    categoryId);
                return StatusCode(
                    StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva");
            }
        }

        [HttpPut("{serviceId}/business")]
        [Authorize(Roles = UserRoles.Business)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddServiceToBusiness(Guid serviceId, [FromQuery] Guid? businessId = null)
        {
            try
            {
                var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
                if (!success)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, errorMessage ?? "Nemate pristup traženom biznisu.");
                }

                var result = await _unitOfWork.Services.AddServiceToBusiness(serviceId, targetBusinessId);

                if (!result)
                {
                    return NotFound("Usluga nije pronađena ili ne može biti dodata vašem preduzeću");
                }

                await _unitOfWork.CompleteAsync();
                return Ok("Usluga je uspešno dodata vašem preduzeću");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom dodavanja usluge {ServiceId} vašem preduzeću", serviceId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva");
            }
        }
    }
}
