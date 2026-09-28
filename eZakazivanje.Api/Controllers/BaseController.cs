using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using eZakazivanje.DataService.Repositories.Interfaces;
using System.Security.Claims;

namespace eZakazivanje.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BaseController : ControllerBase
    {
    protected readonly IUnitOfWork _unitOfWork;
    protected readonly IMapper _mapper;

    public BaseController(IUnitOfWork unitOfWork,IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets and validates the business ID from query parameter or JWT token.
    /// Validates that the user owns the specified business.
    /// </summary>
    /// <param name="businessIdFromQuery">Optional business ID from query parameter</param>
    /// <returns>Tuple: (success, businessId, errorMessage)</returns>
    protected async Task<(bool Success, Guid BusinessId, string? ErrorMessage)> GetAndValidateBusinessIdAsync(Guid? businessIdFromQuery = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return (false, Guid.Empty, "User ID not found in token");
        }

        Guid targetBusinessId;

        // If businessId is provided in query, use it
        if (businessIdFromQuery.HasValue)
        {
            targetBusinessId = businessIdFromQuery.Value;
        }
        else
        {
            // Otherwise, use the primary business from token
            var primaryBusinessId = User.FindFirst("BusinessId")?.Value;
            if (string.IsNullOrEmpty(primaryBusinessId) || !Guid.TryParse(primaryBusinessId, out targetBusinessId))
            {
                return (false, Guid.Empty, "Nepostoji preduzeća povezano sa ovim nalogom");
            }
        }

        // Validate that the user owns this business
        var business = await _unitOfWork.Bussinesses.GetById(targetBusinessId);
        if (business == null)
        {
            return (false, Guid.Empty, $"Preduzeće sa ID {targetBusinessId} nije pronađeno");
        }

        if (business.UserId != userId)
        {
            return (false, Guid.Empty, "Nemate pristup ovom biznisu");
        }

        return (true, targetBusinessId, null);
    }

    /// <summary>
    /// Gets all business IDs that the current user owns from the JWT token
    /// </summary>
    protected List<Guid> GetUserBusinessIds()
    {
        var businessIdsClaim = User.FindFirst("BusinessIds")?.Value;
        if (string.IsNullOrEmpty(businessIdsClaim))
        {
            return new List<Guid>();
        }

        return businessIdsClaim
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(id => Guid.TryParse(id.Trim(), out _))
            .Select(id => Guid.Parse(id.Trim()))
            .ToList();
    }
    }
}
