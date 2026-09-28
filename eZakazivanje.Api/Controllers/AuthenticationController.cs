using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

using eZakazivanje.DataService.BackgroundServices.Services;
using eZakazivanje.DataService.Services;
using Microsoft.EntityFrameworkCore;
using eZakazivanje.DataService.Data;
using eZakazivanje.Entity.DTOS.Request;
using eZakazivanje.Entity.DTOS.Response;

namespace eZakazivanje.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthenticationController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILoggerFactory _loggerFactory;
        private readonly EmailService _emailService;
        private readonly AppDbContext _context;

        public AuthenticationController(
            IAuthService authService, 
            ILogger<AuthenticationController> logger,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            EmailService emailService,
            AppDbContext context)
        {
            _authService = authService;
            _logger = logger;
            _userManager = userManager;
            _configuration = configuration;
            _loggerFactory = loggerFactory;
            _emailService = emailService;
            _context = context;
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login(LoginModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest("Nevalidan payload");
                var (status, message) = await _authService.Login(model);
                if (status == 0)
                    return BadRequest(message);
                return Ok(message);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// Registers a new user account
        /// </summary>
        /// <param name="model">User registration data</param>
        /// <param name="role">User role (User, Business, or Admin). Defaults to User if not specified.</param>
        /// <returns>
        /// 201 Created: User successfully registered
        /// 400 Bad Request: Invalid data or password requirements not met
        /// </returns>
        /// <remarks>
        /// Password Requirements:
        /// - Minimum 8 characters
        /// - At least one uppercase letter (A-Z)
        /// - At least one lowercase letter (a-z)
        /// - At least one digit (0-9)
        /// </remarks>
        [HttpPost]
        [Route("registeration")]
        public async Task<IActionResult> Register(RegistraionModel model, string role)
        {
            role ??= UserRoles.User;
            if(role != UserRoles.Business && role != UserRoles.User && role != UserRoles.Admin) 
                return BadRequest("Nevalidna uloga");
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest("Nevalidan payload");
                var (status, message, userId) = await _authService.Registration(model, role);
                if (status == 0)
                {
                    return BadRequest(message);
                }
                return CreatedAtAction(nameof(Register), new { id = userId, message = message });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogWarning(dbEx, "Database error during registration");
                
                // Check for duplicate key violations (PostgreSQL error code 23505)
                var innerException = dbEx.InnerException;
                if (innerException != null)
                {
                    var exceptionType = innerException.GetType();
                    var sqlStateProperty = exceptionType.GetProperty("SqlState");
                    var constraintNameProperty = exceptionType.GetProperty("ConstraintName");
                    
                    if (sqlStateProperty != null && constraintNameProperty != null)
                    {
                        var sqlState = sqlStateProperty.GetValue(innerException)?.ToString();
                        var constraintName = constraintNameProperty.GetValue(innerException)?.ToString();
                        
                        if (sqlState == "23505") // Unique constraint violation
                        {
                            if (constraintName == "UserNameIndex")
                            {
                                return BadRequest("Korisničko ime već postoji. Molimo izaberite drugo korisničko ime.");
                            }
                            if (constraintName?.Contains("Email", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                return BadRequest("Email adresa već postoji. Molimo koristite drugu email adresu.");
                            }
                            return BadRequest("Korisnik sa ovim podacima već postoji.");
                        }
                    }
                }
                
                return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom registracije. Molimo pokušajte ponovo.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration");
                return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom registracije. Molimo pokušajte ponovo.");
            }
        }

        /// <summary>
        /// Social media login (Google, Facebook, Apple, Microsoft)
        /// </summary>
        /// <param name="request">Social login request with provider and token</param>
        /// <returns>JWT token and user information</returns>
        [HttpPost]
        [Route("social-login")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SocialLogin([FromBody] SocialLoginRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var (success, message, response) = await _authService.SocialLoginAsync(request);

                if (!success)
                {
                    return BadRequest(new { message });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during social login");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during social login" });
            }
        }

        /// <summary>
        /// Get OAuth login URLs for redirecting users to social providers
        /// </summary>
        [HttpGet]
        [Route("oauth-urls")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetOAuthUrls()
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                
                var urls = new
                {
                    Google = $"{baseUrl}/api/authentication/signin-google",
                    Facebook = $"{baseUrl}/api/authentication/signin-facebook",
                    Microsoft = $"{baseUrl}/api/authentication/signin-microsoft"
                };

                return Ok(urls);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OAuth URLs");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred" });
            }
        }

        [HttpGet("roles/{userId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUserRoles(string userId)
        {
            try
            {
                // Check if the requesting user is either an admin or the user themselves
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (currentUserId != userId && !User.IsInRole(UserRoles.Admin))
                {
                    return Forbid("Niste autorizovani da pristupite ovim informacijama");
                }

                var user = await _userManager.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);
                
                if (user == null)
                {
                    return NotFound($"Nije pronađen korisnik sa ID {userId}");
                }

                var roles = await _authService.GetUserRoles(userId);
                
                if (!roles.Any())
                {
                    return NotFound($"Nije pronađena uloga za korisnika sa ID {userId}");
                }

                return Ok(new { UserId = userId, Roles = roles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom dobijanja uloga za korisnika");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        [HttpPost("forgot-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    // Don't reveal that the user does not exist
                    return Ok(new { message = "Ako je vaša email adresa registrovan, dobićete link za resetovanje lozinke." });
                }

                // Generate password reset token
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                var resetLink = EmailAppUrls.BuildPasswordResetLink(
                    _configuration,
                    user.Email ?? string.Empty,
                    token,
                    $"{Request.Scheme}://{Request.Host}");

                var (resetSubject, emailBody) = NotificationMessages.PasswordResetEmail(
                    user.PreferredLanguage,
                    user.FirstName,
                    resetLink);

                if (string.IsNullOrEmpty(user.Email))
                {
                    return BadRequest("Email korisnika nije postavljen");
                }

                var emailSent = await _emailService.SendEmailAsync(
                    user.Email,
                    resetSubject,
                    emailBody);

                if (!emailSent)
                {
                    _logger.LogWarning("Greška prilikom slanja email-a za resetovanje lozinke");
                    return StatusCode(500, "Došlo je do greške prilikom slanja email-a za resetovanje lozinke.");
                }

                return Ok(new { message = "Ako je vaša email adresa registrovan, dobićete link za resetovanje lozinke." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom resetovanja lozinke");
                return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        [HttpPost("reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return BadRequest("Nevalidan zahtev");
                }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (result.Succeeded)
                {
                    return Ok(new { message = "Lozinka je uspešno resetovana." });
                }

                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
                }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom resetovanja lozinke");
                return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        [HttpPost("send-verification-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendVerificationEmail([FromBody] string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null || string.IsNullOrEmpty(user.Email))
                {
                    return Ok(new { message = "Ako je vaša email adresa registrovana, dobićete link za verifikaciju." });
                }

                if (user.EmailConfirmed)
                {
                    return BadRequest("Email je već verifikovan.");
                }

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var verificationLink = EmailAppUrls.BuildVerifyEmailLink(
                    _configuration,
                    user.Email ?? string.Empty,
                    token,
                    $"{Request.Scheme}://{Request.Host}");

                var (verifySubject, emailBody) = NotificationMessages.EmailVerification(
                    user.PreferredLanguage,
                    user.FirstName,
                    verificationLink,
                    registrationContext: false);

                await _emailService.SendEmailAsync(
                    user.Email ?? throw new InvalidOperationException("Email korisnika nije postavljen"),
                    verifySubject,
                    emailBody);

                return Ok(new { message = "If your email address is registered and not verified, you will receive a verification link." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom slanja email-a za verifikaciju");
                return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        [HttpPost("verify-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return BadRequest("Nevalidan zahtev");
                }

                var result = await _userManager.ConfirmEmailAsync(user, request.Token);
                if (result.Succeeded)
                {
                    // If this is a Business user, auto-approve/activate their pending businesses.
                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains(UserRoles.Business))
                    {
                        var pendingBusinesses = await _context.Businesses
                            .Where(b =>
                                b.UserId == user.Id &&
                                (b.ApprovalStatus == BusinessApprovalStatus.Pending || b.IsActive != true))
                            .ToListAsync();

                        if (pendingBusinesses.Any())
                        {
                            foreach (var b in pendingBusinesses)
                            {
                                b.ApprovalStatus = BusinessApprovalStatus.Approved;
                                b.IsActive = true;
                                b.UpdatedAt = DateTime.UtcNow;
                            }

                            await _context.SaveChangesAsync();
                        }
                    }

                    return Ok(new { message = "Email je uspešno verifikovan." });
                }

                return BadRequest("Verifikacija emaila nije uspela.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom verifikacije emaila");
                return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        [HttpGet("verify-email-page")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyEmailPage([FromQuery] string email, [FromQuery] string token)
        {
            try
            {
                _logger.LogInformation("Starting email verification");

                var decodedEmail = DecodeEmailLinkQueryParam(email);
                var decodedToken = DecodeEmailLinkQueryParam(token);
                if (string.IsNullOrEmpty(decodedEmail) || string.IsNullOrEmpty(decodedToken))
                {
                    _logger.LogWarning("Missing or empty email/token in verification query");
                    return GenerateHtmlResponse("Invalid Request", "The verification link is invalid or incomplete.", false);
                }

                var user = await _userManager.FindByEmailAsync(decodedEmail);
                if (user == null)
                {
                    _logger.LogWarning("User not found");
                    return GenerateHtmlResponse("Invalid Request", "The verification link is invalid.", false);
                }

                _logger.LogInformation("User found");

                IdentityResult result;
                try
                {
                    result = await _userManager.ConfirmEmailAsync(user, decodedToken);
                }
                catch (Exception ex) when (ex is CryptographicException or FormatException)
                {
                    _logger.LogWarning(ex, "Email confirmation token could not be read (wrong keys, corrupt link, or truncated URL)");
                    return GenerateHtmlResponse("Verification Failed", "Email verification failed. Please try again or contact support.", false);
                }

                if (result.Succeeded)
                {
                    _logger.LogInformation("Email verification succeeded");

                    // Post-confirm actions should never block email verification from succeeding.
                    // In production this can fail due to transient DB issues; the user should still see "Email Verified".
                    try
                    {
                        // If this is a Business user, auto-approve/activate their pending businesses.
                        var roles = await _userManager.GetRolesAsync(user);
                        if (roles.Contains(UserRoles.Business))
                        {
                            var pendingBusinesses = await _context.Businesses
                                // Avoid bool?.GetValueOrDefault(): EF can't translate it (seen in Sliplane logs).
                                // Treat NULL as inactive.
                                .Where(b =>
                                    b.UserId == user.Id &&
                                    (b.ApprovalStatus == BusinessApprovalStatus.Pending || b.IsActive != true))
                                .ToListAsync();

                            if (pendingBusinesses.Any())
                            {
                                foreach (var b in pendingBusinesses)
                                {
                                    b.ApprovalStatus = BusinessApprovalStatus.Approved;
                                    b.IsActive = true;
                                    b.UpdatedAt = DateTime.UtcNow;
                                }

                                await _context.SaveChangesAsync();
                                _logger.LogInformation("Activated {Count} pending businesses for user {UserId}", pendingBusinesses.Count, user.Id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Post-confirm business activation failed for user {UserId}", user.Id);
                    }

                    return GenerateHtmlResponse("Email Verified", "Your email has been successfully verified. You can now log in to your account.", true);
                }

                _logger.LogWarning("Email verification failed");
                return GenerateHtmlResponse("Verification Failed", "Email verification failed. Please try again or contact support.", false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email verification");
                var requestId = HttpContext.TraceIdentifier;
                return GenerateHtmlResponse("Error", $"An error occurred while processing your request. RequestId: {requestId}", false);
            }
        }

        /// <summary>
        /// Query values are usually decoded once by ASP.NET; some mail clients double-encode.
        /// Never throw: malformed % sequences must not become a generic 500-style HTML error.
        /// </summary>
        private static string? DecodeEmailLinkQueryParam(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var s = value.Trim();
            for (var i = 0; i < 2; i++)
            {
                try
                {
                    var next = Uri.UnescapeDataString(s);
                    if (next == s)
                        break;
                    s = next;
                }
                catch (UriFormatException)
                {
                    break;
                }
            }

            return s;
        }

        private IActionResult GenerateHtmlResponse(string title, string message, bool success)
        {
    
            var html = $@"
        <!DOCTYPE html>
<html lang='en'>
        <head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>{title}</title>
            <style>
                body {{
                    font-family: Arial, sans-serif;
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    height: 100vh;
                    margin: 0;
                    background-color: #f5f5f5;
                }}
                .container {{
                    text-align: center;
                    padding: 2rem;
                    background-color: white;
                    border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
            max-width: 500px;
            width: 90%;
        }}
        .title {{
            color: {(success ? "#28a745" : "#dc3545")};
            margin-bottom: 1rem;
        }}
        .message {{
            color: #666;
            margin-bottom: 2rem;
        }}
            </style>
        </head>
        <body>
            <div class='container'>
        <h1 class='title'>{title}</h1>
        <p class='message'>{message}</p>
            </div>
        </body>
        </html>";
                return Content(html, "text/html");
            }

        [HttpPost("resend-verification")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    // Don't reveal that the user doesn't exist
                    return Ok(new { message = "Ako je vaša email adresa registrovana i nije verifikovana, dobićete link za verifikaciju." });
                }

                if (user.EmailConfirmed)
                {
                    return BadRequest(new { message = "Email je već verifikovan." });
                }

                // Generate verification token and send email
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var verificationLink = EmailAppUrls.BuildVerifyEmailLink(
                    _configuration,
                    user.Email ?? string.Empty,
                    token,
                    $"{Request.Scheme}://{Request.Host}");

                var (verifySubject, emailBody) = NotificationMessages.EmailVerification(
                    user.PreferredLanguage,
                    user.FirstName,
                    verificationLink,
                    registrationContext: false);

                await _emailService.SendEmailAsync(
                    user.Email ?? throw new InvalidOperationException("Email korisnika nije postavljen"),
                    verifySubject,
                    emailBody);

                return Ok(new { message = "If your email address is registered and not verified, you will receive a verification link." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom ponovnog slanja email-a za verifikaciju");
                return StatusCode(500, "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        [HttpGet("users")]
        [Authorize(Roles = UserRoles.Admin)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _authService.GetAllUsers();
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom dobijanja svih korisnika");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        [HttpGet("approve-business")]
        [Authorize(Roles = UserRoles.Admin)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ApproveBusiness([FromQuery] string email, [FromQuery] string? businessId = null)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    return GenerateHtmlResponse("Greška", "Korisnik nije pronađen.", false);
                }

                Bussiness? business;
                
                if (!string.IsNullOrEmpty(businessId) && Guid.TryParse(businessId, out var businessIdGuid))
                {
                    // Approve specific business by ID (for additional businesses)
                    business = await _context.Businesses
                        .FirstOrDefaultAsync(b => b.Id == businessIdGuid && b.UserId == user.Id);
                }
                else
                {
                    // Backward compatibility: find first pending business for user (or first business if none pending)
                    business = await _context.Businesses
                        .Where(b => b.UserId == user.Id)
                        .OrderByDescending(b => b.ApprovalStatus == BusinessApprovalStatus.Pending)
                        .ThenBy(b => b.CreatedAt)
                        .FirstOrDefaultAsync();
                }
                
                if (business == null)
                {
                    return GenerateHtmlResponse("Greška", "Biznis nije pronađen.", false);
                }

                business.ApprovalStatus = BusinessApprovalStatus.Approved;
                business.IsActive = true;
                await _context.SaveChangesAsync();

                // Send verification email to the business user
                var verificationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var verificationLink = EmailAppUrls.BuildVerifyEmailLink(
                    _configuration,
                    user.Email ?? string.Empty,
                    verificationToken,
                    $"{Request.Scheme}://{Request.Host}");

                var (approvedSubject, approvedBody) = NotificationMessages.BusinessApprovedEmail(
                    user.PreferredLanguage,
                    user.FirstName,
                    business.Name ?? string.Empty,
                    verificationLink);

                await _emailService.SendEmailAsync(
                    user.Email ?? throw new InvalidOperationException("Email korisnika nije postavljen"),
                    approvedSubject,
                    approvedBody);

                return GenerateHtmlResponse("Biznis odobren", "Biznis je uspešno odobren i verifikacioni email je poslat.", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška prilikom odobravanja biznisa");
                return GenerateHtmlResponse("Greška", "Došlo je do greške prilikom obrade vašeg zahteva.", false);
            }
        }

        [HttpGet("reject-business")]
        [Authorize(Roles = UserRoles.Admin)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RejectBusiness([FromQuery] string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    return GenerateHtmlResponse("Greška", "Korisnik nije pronađen.", false);
                }

                var business = await _context.Businesses.FindAsync(Guid.Parse(user.Id));
                if (business == null)
                {
                    return GenerateHtmlResponse("Greška", "Biznis nije pronađen.", false);
                }

                business.ApprovalStatus = BusinessApprovalStatus.Rejected;
                business.IsActive = false;
                await _context.SaveChangesAsync();

                // Send rejection email to the business user
                var (rejectedSubject, rejectedBody) = NotificationMessages.BusinessRegistrationRejectedEmail(
                    user.PreferredLanguage,
                    user.FirstName);

                await _emailService.SendEmailAsync(
                    user.Email ?? throw new InvalidOperationException("Email korisnika nije postavljen"),
                    rejectedSubject,
                    rejectedBody);

                return GenerateHtmlResponse("Biznis odbijen", "Biznis je uspešno odbijen.", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom odbijanja biznisa");
                return GenerateHtmlResponse("Greška", "Došlo je do greške prilikom obrade vašeg zahteva.", false);
            }
        }

        [HttpGet("reset-password-page")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPasswordPage([FromQuery] string email, [FromQuery] string token)
        {
            try
            {
                _logger.LogInformation("Starting password reset page verification");

                var decodedEmail = DecodeEmailLinkQueryParam(email);
                var decodedToken = DecodeEmailLinkQueryParam(token);
                if (string.IsNullOrEmpty(decodedEmail) || string.IsNullOrEmpty(decodedToken))
                {
                    _logger.LogWarning("Missing or empty email/token in reset-password query");
                    return GenerateHtmlResponse("Invalid Request", "The password reset link is invalid or incomplete.", false);
                }

                var user = await _userManager.FindByEmailAsync(decodedEmail);
                if (user == null)
                {
                    _logger.LogWarning("User not found");
                    return GenerateHtmlResponse("Invalid Request", "The password reset link is invalid.", false);
                }

                _logger.LogInformation("User found");

                bool isValid;
                try
                {
                    isValid = await _userManager.VerifyUserTokenAsync(
                        user,
                        _userManager.Options.Tokens.PasswordResetTokenProvider,
                        UserManager<ApplicationUser>.ResetPasswordTokenPurpose,
                        decodedToken);
                }
                catch (Exception ex) when (ex is CryptographicException or FormatException)
                {
                    _logger.LogWarning(ex, "Password reset token could not be read (wrong keys, corrupt link, or truncated URL)");
                    return GenerateHtmlResponse("Invalid Token", "The password reset token is invalid or has expired. Please request a new reset link.", false);
                }

                if (!isValid)
                {
                    _logger.LogWarning("Invalid reset token");
                    return GenerateHtmlResponse("Invalid Token", "The password reset token is invalid or has expired. Please request a new reset link.", false);
                }

                var html = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Password Reset</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background-color: #f5f5f5;
        }}
        .container {{
            text-align: center;
            padding: 2rem;
            background-color: white;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
            max-width: 500px;
            width: 90%;
        }}
        .title {{
            color: #333;
            margin-bottom: 1.5rem;
        }}
        .form-group {{
            margin-bottom: 1rem;
            text-align: left;
        }}
        label {{
            display: block;
            margin-bottom: 0.5rem;
            color: #666;
        }}
        input {{
            width: 100%;
            padding: 0.5rem;
            border: 1px solid #ddd;
            border-radius: 4px;
            box-sizing: border-box;
        }}
        button {{
            background-color: #007bff;
            color: white;
            border: none;
            padding: 0.75rem 1.5rem;
            border-radius: 4px;
            cursor: pointer;
            font-size: 1rem;
            width: 100%;
            margin-top: 1rem;
        }}
        button:hover {{
            background-color: #0056b3;
        }}
        .error {{
            color: #dc3545;
            margin-top: 0.5rem;
            display: none;
        }}
        .success {{
            color: #28a745;
            margin-top: 0.5rem;
            display: none;
        }}
        .description {{
            color: #666;
            margin-bottom: 1.5rem;
            font-size: 0.95rem;
        }}
        .requirements {{
            background-color: #f8f9fa;
            padding: 1rem;
            border-radius: 4px;
            margin-bottom: 1rem;
            text-align: left;
            font-size: 0.9rem;
        }}
        .requirements ul {{
            margin: 0.5rem 0 0 1.5rem;
            padding: 0;
        }}
        .requirements li {{
            margin-bottom: 0.25rem;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <h1 class='title'>Password Reset</h1>
        <p class='description'>Please enter your new password. The password must be secure and easy to remember.</p>
        
        <div class='requirements'>
            <strong>Password Requirements:</strong>
            <ul>
                <li>Minimum 8 characters</li>
                <li>At least one uppercase letter (A-Z)</li>
                <li>At least one lowercase letter (a-z)</li>
                <li>At least one digit (0-9)</li>
            </ul>
        </div>
        
        <form id='resetForm'>
            <div class='form-group'>
                <label for='newPassword'>New Password</label>
                <input type='password' id='newPassword' name='newPassword' required minlength='8' placeholder='Enter new password'>
            </div>
            <div class='form-group'>
                <label for='confirmPassword'>Confirm Password</label>
                <input type='password' id='confirmPassword' name='confirmPassword' required minlength='8' placeholder='Confirm new password'>
            </div>
            <button type='submit'>Reset Password</button>
            <div id='error' class='error'></div>
            <div id='success' class='success'></div>
        </form>
        <p style='margin-top: 1.5rem; color: #999; font-size: 0.85rem;'>
            After successful reset, you will be able to log in with your new password.
        </p>
    </div>

    <script>
        document.getElementById('resetForm').addEventListener('submit', async (e) => {{
            e.preventDefault();
            
            const newPassword = document.getElementById('newPassword').value;
            const confirmPassword = document.getElementById('confirmPassword').value;
            const errorDiv = document.getElementById('error');
            const successDiv = document.getElementById('success');
            
            if (newPassword !== confirmPassword) {{
                errorDiv.textContent = 'Passwords do not match';
                errorDiv.style.display = 'block';
                successDiv.style.display = 'none';
                return;
            }}
            
            try {{
                const response = await fetch('/api/Authentication/reset-password', {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json'
                    }},
                    body: JSON.stringify({{
                        email: '{decodedEmail}',
                        token: '{decodedToken}',
                        newPassword: newPassword,
                        confirmPassword: confirmPassword
                    }})
                }});
                
                const data = await response.json();
                
                if (response.ok) {{
                    successDiv.textContent = 'Password has been successfully reset. You can now log in.';
                    successDiv.style.display = 'block';
                    errorDiv.style.display = 'none';
                    document.getElementById('resetForm').reset();
                }} else {{
                    errorDiv.textContent = data.message || 'An error occurred while resetting the password';
                    errorDiv.style.display = 'block';
                    successDiv.style.display = 'none';
                }}
            }} catch (error) {{
                errorDiv.textContent = 'An error occurred while resetting the password';
                errorDiv.style.display = 'block';
                successDiv.style.display = 'none';
            }}
        }});
    </script>
</body>
</html>";

                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset page verification");
                return GenerateHtmlResponse("Error", "An error occurred while processing your request.", false);
            }
        }

        /*
        [HttpDelete("clear-database")]
        [Authorize(Roles = UserRoles.Admin)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ClearDatabase()
        {
            try
            {
                // Helper function to check if table exists and truncate it
                async Task TruncateTableIfExists(string tableName)
                {
                    var tableExists = await _context.Database.ExecuteSqlRawAsync(
                        $"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = '{tableName.ToLower()}')");
                    
                    if (tableExists == 1)
                    {
                        await _context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{tableName}\" CASCADE");
                        _logger.LogInformation($"Successfully truncated table {tableName}");
                    }
                    else
                    {
                        _logger.LogInformation($"Table {tableName} does not exist, skipping");
                    }
                }

                // Delete all data from all tables
                await TruncateTableIfExists("AspNetUserRoles");
                await TruncateTableIfExists("AspNetUserClaims");
                await TruncateTableIfExists("AspNetUserLogins");
                await TruncateTableIfExists("AspNetUserTokens");
                await TruncateTableIfExists("AspNetRoleClaims");
                await TruncateTableIfExists("AspNetRoles");
                await TruncateTableIfExists("AspNetUsers");
                await TruncateTableIfExists("Businesses");
                await TruncateTableIfExists("Appointments");
                await TruncateTableIfExists("Services");
                await TruncateTableIfExists("Reviews");
                await TruncateTableIfExists("Notifications");
                await TruncateTableIfExists("Employees");
                await TruncateTableIfExists("EmployeeServices");
                await TruncateTableIfExists("BusinessHours");
                await TruncateTableIfExists("BusinessCategories");
                await TruncateTableIfExists("BusinessLocations");
                await TruncateTableIfExists("BusinessImages");
                await TruncateTableIfExists("ServiceCategories");
                await TruncateTableIfExists("ServiceImages");
                await TruncateTableIfExists("AppointmentStatuses");
                await TruncateTableIfExists("AppointmentNotes");

                // Delete all uploaded images
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (Directory.Exists(uploadsPath))
                {
                    var files = Directory.GetFiles(uploadsPath, "*.*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                            _logger.LogInformation($"Successfully deleted file: {file}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete file: {File}", file);
                        }
                    }

                    // Delete empty directories
                    var directories = Directory.GetDirectories(uploadsPath, "*", SearchOption.AllDirectories)
                        .OrderByDescending(d => d.Length); // Delete deepest directories first
                    foreach (var dir in directories)
                    {
                        try
                        {
                            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                            {
                                Directory.Delete(dir);
                                _logger.LogInformation($"Successfully deleted empty directory: {dir}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete directory: {Directory}", dir);
                        }
                    }
                }

                return Ok(new { message = "Database and uploaded files cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing database and files");
                return StatusCode(500, "An error occurred while clearing the database and files");
            }
        }
        */
    }
    
}
