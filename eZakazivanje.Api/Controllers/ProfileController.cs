using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.DataService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Services;
using eZakazivanje.Entity.DTOS.Response;

namespace eZakazivanje.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ProfileController> _logger;
        private readonly IAuthService _authService;
        private readonly AppDbContext _dbContext;
        private readonly IUserRepository _userRepository;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            ILogger<ProfileController> logger,
            IAuthService authService,
            AppDbContext dbContext,
            IUserRepository userRepository)
        {
            _userManager = userManager;
            _logger = logger;
            _authService = authService;
            _dbContext = dbContext;
            _userRepository = userRepository;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProfileResponse>> GetProfile()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    return NotFound("Korisnik nije pronađen");
                }

                var roles = await _userManager.GetRolesAsync(user);

                var profile = new ProfileResponse
                {
                    Id = user.Id,
                    Name = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    Username = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Roles = roles.ToList(),
                    ImageUrl = user.ImageUrl ?? string.Empty,
                    PreferredLanguage = string.IsNullOrWhiteSpace(user.PreferredLanguage) ? "en" : user.PreferredLanguage
                };

                if (roles.Contains(UserRoles.Business))
                {
                    profile.BusinessName = user.BusinessName;
                    profile.BusinessDescription = user.BusinessDescription;
                }

                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja profila za korisnika");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva");
            }
        }

        [HttpPost("fcm-token")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    return NotFound("Korisnik nije pronađen");
                }

                user.FCMToken = request.Token;
                if (!string.IsNullOrWhiteSpace(request.Language))
                    user.PreferredLanguage = NotificationMessages.NormalizeLanguage(request.Language);

                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation("Ažuriran FCM token za korisnika {UserId}", userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom ažuriranja FCM tokena za korisnika");
                return StatusCode(500, "Došlo je do greške prilikom ažuriranja FCM tokena");
            }
        }

        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteProfile()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    return NotFound("Korisnik nije pronađen");
                }

                // Delete user's profile image if exists
                if (!string.IsNullOrEmpty(user.ImageUrl))
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ImageUrl);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                // Delete the user and their appointments
                var result = await _userRepository.DeleteUserAsync(Guid.Parse(userId));
                if (!result)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, 
                        "Došlo je do greške prilikom brisanja profila");
                }

                _logger.LogInformation("Profil korisnika {UserId} je uspešno obrisan", userId);
                return Ok("Profil je uspešno obrisan");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom brisanja profila korisnika");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva");
            }
        }

        [HttpGet("{businessId}/user")]
        [Authorize(Roles = UserRoles.Admin)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BusinessUserDetailsDto>> GetBusinessUserByBusinessId(Guid businessId)
        {
            try
            {
                var businessDetails = await _userRepository.GetBusinessUserDetailsByBusinessIdAsync(businessId);
                
                if (businessDetails == null)
                {
                    return NotFound("Biznis ili povezani korisnik nije pronađen");
                }

                // Get user roles using UserManager (since repository doesn't have access to it)
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == businessDetails.UserId);
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    businessDetails.Roles = roles.ToList();
                }

                _logger.LogInformation("Admin retrieved user information for business");

                return Ok(businessDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom dobijanja korisničkih informacija za biznis");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva");
            }
        }

        [HttpDelete("admin/users/{userId:guid}")]
        [Authorize(Roles = UserRoles.Admin)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AdminDeleteUserById(Guid userId)
        {
            try
            {
                var userIdString = userId.ToString();

                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userIdString);
                if (user == null)
                {
                    return NotFound("Korisnik nije pronađen");
                }

                // Guard: admin may delete user ONLY if user owns 0 businesses
                var ownsAnyBusiness = await _dbContext.Businesses.AnyAsync(b => b.UserId == user.Id);
                if (ownsAnyBusiness)
                {
                    return BadRequest("Nije moguće obrisati korisnika jer poseduje biznis(e). Prvo obrišite ili prebacite biznis na drugog korisnika.");
                }

                // Delete user's profile image if exists
                if (!string.IsNullOrEmpty(user.ImageUrl))
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ImageUrl);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                // Delete the user and their appointments
                var result = await _userRepository.DeleteUserAsync(userId);
                if (!result)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        "Došlo je do greške prilikom brisanja korisnika");
                }

                _logger.LogInformation("Admin obrisao korisnika {UserId}", userIdString);
                return Ok("Korisnik je uspešno obrisan");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom admin brisanja korisnika {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "Došlo je do greške prilikom obrade vašeg zahteva");
            }
        }

        public class UpdateFcmTokenRequest
        {
            public string Token { get; set; } = string.Empty;
            /// <summary>Optional. e.g. "en", "sr". Stored for localized push notifications.</summary>
            public string? Language { get; set; }
        }
    }
} 