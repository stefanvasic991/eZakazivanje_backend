using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.BackgroundServices.Services;
using Microsoft.EntityFrameworkCore;
using eZakazivanje.DataService.Data;
using eZakazivanje.Entity.DTOS.Request;
using eZakazivanje.Entity.DTOS.Response;
using Google.Apis.Auth;
using System.Text.Json;
using eZakazivanje.DataService.Services;


namespace eZakazivanje.DataService.Repositories;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly RoleManager<IdentityRole> roleManager;
    private readonly IConfiguration _configuration;
    private readonly IBussinessRepository _businessRepository;
    private readonly ILogger<AuthService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly AppDbContext _context;
    private readonly SignInManager<ApplicationUser> signInManager;
    private readonly EmailService _emailService;
    private readonly BusinessApprovalLinkService _businessApprovalLinkService;

    public AuthService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration, IBussinessRepository businessRepository, ILogger<AuthService> logger, ILoggerFactory loggerFactory, AppDbContext context, SignInManager<ApplicationUser> signInManager, EmailService emailService, BusinessApprovalLinkService businessApprovalLinkService)
    {
            this.userManager = userManager;
            this.roleManager = roleManager;
            _configuration = configuration;
            _businessRepository = businessRepository;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _context = context;
            this.signInManager = signInManager;
            _emailService = emailService;
            _businessApprovalLinkService = businessApprovalLinkService;
    }

    public async Task<(int, string)> Login(LoginModel model)
    {
        if (model == null)
            return (0, "Neispravan zahtev za logovanje");

        if (string.IsNullOrEmpty(model.Username))
            return (0, "Korisničko ime je obavezno polje");

        if (string.IsNullOrEmpty(model.Password))
            return (0, "Lozinka je obavezno polje");

        var user = await userManager.FindByNameAsync(model.Username);
        if (user == null)
            return (0, "Neispravno korisničko ime");
        if (!await userManager.CheckPasswordAsync(user, model.Password))
            return (0, "Neispravna lozinka");
        
        if (!user.EmailConfirmed)
            return (0, "Molimo proverite vašu email adresu prije logovanja");

        var userRoles = await userManager.GetRolesAsync(user);
        var authClaims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        foreach (var userRole in userRoles)
        {
            authClaims.Add(new Claim(ClaimTypes.Role, userRole));
            
            if (userRole == UserRoles.Business)
            {
                // Get all businesses owned by this user
                var userBusinesses = await _context.Businesses
                    .Where(b => b.UserId == user.Id)
                    .OrderByDescending(b => b.ApprovalStatus == BusinessApprovalStatus.Approved)
                    .ThenBy(b => b.CreatedAt)
                    .ToListAsync();

                if (userBusinesses.Any())
                {
                    // Add all business IDs as a comma-separated list
                    var businessIds = string.Join(",", userBusinesses.Select(b => b.Id.ToString()));
                    authClaims.Add(new Claim("BusinessIds", businessIds));

                    // Add the first approved business (or first business if none approved) as primary BusinessId for backward compatibility
                    var primaryBusiness = userBusinesses.FirstOrDefault(b => b.ApprovalStatus == BusinessApprovalStatus.Approved && b.IsActive == true)
                        ?? userBusinesses.FirstOrDefault();
                    
                    if (primaryBusiness != null)
                    {
                        authClaims.Add(new Claim("BusinessId", primaryBusiness.Id.ToString()));
                    }
                }
            }
        }

        string token = GenerateToken(authClaims);
        return (1, token);
    }

    public async Task<(int status, string message, string? userId)> Registration(RegistraionModel model, string role)
    {
        if (model == null)
            return (0, "Neispravan zahtev za registraciju", null);

        if (string.IsNullOrEmpty(model.Username))
            return (0, "Korisničko ime je obavezno polje", null);

        if (string.IsNullOrEmpty(model.Password))
            return (0, "Lozinka je obavezno polje", null);

        if (string.IsNullOrEmpty(model.Email))
            return (0, "Email je obavezno polje", null);

        var userExists = await userManager.FindByNameAsync(model.Username);
        if (userExists != null)
            return (0, "Korisničko ime već postoji. Molimo izaberite drugo korisničko ime.", null);

        var emailExists = await userManager.FindByEmailAsync(model.Email);
        if (emailExists != null)
            return (0, "Email adresa već postoji. Molimo koristite drugu email adresu.", null);

        ApplicationUser user = new()
        {
            Email = model.Email,
            SecurityStamp = Guid.NewGuid().ToString(),
            UserName = model.Username,
            FirstName = model.FirstName ?? string.Empty,
            LastName = model.LastName ?? string.Empty,
            EmailConfirmed = false,
            ImageUrl = string.Empty,
            PhoneNumber = model.PhoneNumber ?? string.Empty
        };

        if (role == UserRoles.Business)
        {
            // Signup still behaves like normal user signup (email verification),
            // but we also create the initial business immediately (without admin approval).
            user.BusinessName = model.BusinessName;
            user.BusinessDescription = model.BusinessDescription;

            var createUserResult = await userManager.CreateAsync(user, model.Password);
            if (!createUserResult.Succeeded)
            {
                var errors = string.Join(", ", createUserResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Business user creation failed: {Errors}", errors);
                return (0, $"Kreiranje korisnika nije uspelo: {errors}", null);
            }

            if (!await roleManager.RoleExistsAsync(UserRoles.Business))
                await roleManager.CreateAsync(new IdentityRole(UserRoles.Business));

            await userManager.AddToRoleAsync(user, UserRoles.Business);

            // Create the initial business right away (saved in DB by CreateBusiness).
            // Email verification still required for the account; post-verify step may touch pending/inactive rows only.
            var business = new Bussiness
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = model.BusinessName,
                Description = model.BusinessDescription,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ApprovalStatus = BusinessApprovalStatus.Approved
            };

            var businessCreated = await _businessRepository.CreateBusiness(business);
            if (!businessCreated)
            {
                await userManager.DeleteAsync(user);
                return (0, "Kreiranje poslovne entitete nije uspelo! Molimo pokušajte ponovo.", null);
            }

            await SendVerificationEmail(user);

            return (1, "Korisnik i biznis su kreirani. Molimo proverite vašu email adresu da biste aktivirali biznis.", user.Id);
        }
        else
        {
            var createUserResult = await userManager.CreateAsync(user, model.Password);
            if (!createUserResult.Succeeded)
            {
                var errors = string.Join(", ", createUserResult.Errors.Select(e => e.Description));
                _logger.LogWarning("User creation failed: {Errors}", errors);
                return (0, $"Kreiranje korisnika nije uspelo: {errors}", null);
            }

            var adminExists = await userManager.GetUsersInRoleAsync(UserRoles.Admin);
            string roleToAssign;

            if (!adminExists.Any())
            {
                if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
                    await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));
                
                roleToAssign = UserRoles.Admin;
            }
            else
            {
                if (!await roleManager.RoleExistsAsync(UserRoles.User))
                    await roleManager.CreateAsync(new IdentityRole(UserRoles.User));
                
                roleToAssign = UserRoles.User;
            }

            await userManager.AddToRoleAsync(user, roleToAssign);
            await SendVerificationEmail(user);

            return (1, $"Korisnik kreiran uspešno sa ulogom. Molimo proverite vašu email adresu da biste verifikovali svoj nalog.", user.Id);
        }
    }

    private string GenerateToken(IEnumerable<Claim> claims)
    {
        var jwtSecret = Environment.GetEnvironmentVariable("JWT__Secret") ?? 
            throw new InvalidOperationException("JWT Secret key is not configured");
        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://zakazime.sliplane.app",
            Audience = "https://zakazime.sliplane.app",
            Expires = DateTime.UtcNow.AddHours(8736),
            SigningCredentials = new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(claims)
        };
        

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public async Task<IEnumerable<string>> GetUserRoles(string userId)
    {
        try
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Korisnik nije pronađen");
                return Enumerable.Empty<string>();
            }

            var roles = await userManager.GetRolesAsync(user);
            _logger.LogInformation("Pronadjena uloga");
            return roles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja uloge");
            return Enumerable.Empty<string>();
        }
    }

    public async Task SendVerificationEmail(ApplicationUser user)
    {
        try
        {
            _logger.LogInformation("Generisanje email verifikacijskog tokena");
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);

            var verificationLink = EmailAppUrls.BuildVerifyEmailLink(
                _configuration,
                user.Email ?? string.Empty,
                token,
                requestBackendFallback: null);
            _logger.LogInformation("Generated verification link: {Link}", verificationLink);

            var (subject, emailBody) = NotificationMessages.EmailVerification(
                user.PreferredLanguage,
                user.FirstName,
                verificationLink,
                registrationContext: true);

            await _emailService.SendEmailAsync(
                user.Email ?? throw new InvalidOperationException("Email korisnika nije postavljen"),
                subject,
                emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom slanja email-a za verifikaciju");
            throw;
        }
    }

    public async Task<IEnumerable<ApplicationUserResponse>> GetAllUsers()
    {
        try
        {
            var users = await userManager.Users.ToListAsync();
            var userResponses = new List<ApplicationUserResponse>();

            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);
                userResponses.Add(new ApplicationUserResponse
                {
                    Id = user.Id,
                    Username = user.UserName ?? string.Empty,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Roles = roles.ToList()
                });
            }

            _logger.LogInformation("Pronadjeni svi korisnici uspešno");
            return userResponses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja svih korisnika");
            return Enumerable.Empty<ApplicationUserResponse>();
        }
    }

    public async Task<(bool Success, string Message, SocialLoginResponse? Response)> SocialLoginAsync(SocialLoginRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrEmpty(request.Provider) || string.IsNullOrEmpty(request.IdToken))
            {
                return (false, "Invalid request. Provider and IdToken are required.", null);
            }

            ApplicationUser? user = null;
            bool isNewUser = false;
            string email = string.Empty;
            string firstName = string.Empty;
            string lastName = string.Empty;
            string? imageUrl = null;
            string externalUserId = string.Empty;

            // Verify token and extract user info based on provider
            switch (request.Provider.ToLower())
            {
                case "google":
                    var googleResult = await VerifyGoogleTokenAsync(request.IdToken);
                    if (!googleResult.Success)
                    {
                        return (false, googleResult.Message, null);
                    }
                    email = googleResult.Email ?? string.Empty;
                    firstName = googleResult.FirstName ?? string.Empty;
                    lastName = googleResult.LastName ?? string.Empty;
                    imageUrl = googleResult.Picture;
                    externalUserId = googleResult.Subject ?? string.Empty;
                    break;

                case "facebook":
                    var facebookResult = await VerifyFacebookTokenAsync(request.AccessToken ?? request.IdToken);
                    if (!facebookResult.Success)
                    {
                        return (false, facebookResult.Message, null);
                    }
                    email = facebookResult.Email ?? string.Empty;
                    firstName = facebookResult.FirstName ?? string.Empty;
                    lastName = facebookResult.LastName ?? string.Empty;
                    imageUrl = facebookResult.Picture;
                    externalUserId = facebookResult.Id ?? string.Empty;
                    break;

                case "apple":
                    var appleResult = await VerifyAppleTokenAsync(request.IdToken);
                    if (!appleResult.Success)
                    {
                        return (false, appleResult.Message, null);
                    }
                    email = appleResult.Email ?? string.Empty;
                    firstName = appleResult.FirstName ?? string.Empty;
                    lastName = appleResult.LastName ?? string.Empty;
                    externalUserId = appleResult.Subject ?? string.Empty;
                    break;

                case "microsoft":
                    var microsoftResult = await VerifyMicrosoftTokenAsync(request.IdToken);
                    if (!microsoftResult.Success)
                    {
                        return (false, microsoftResult.Message, null);
                    }
                    email = microsoftResult.Email ?? string.Empty;
                    firstName = microsoftResult.FirstName ?? string.Empty;
                    lastName = microsoftResult.LastName ?? string.Empty;
                    imageUrl = microsoftResult.Picture;
                    externalUserId = microsoftResult.Subject ?? string.Empty;
                    break;

                default:
                    return (false, $"Unsupported provider: {request.Provider}", null);
            }

            if (string.IsNullOrEmpty(email))
            {
                return (false, "Email is required but not provided by the social provider.", null);
            }

            // Check if user exists by email
            user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                // Create new user
                isNewUser = true;
                var role = request.Role ?? UserRoles.User;

                user = new ApplicationUser
                {
                    UserName = email, // Use email as username
                    Email = email,
                    EmailConfirmed = true, // Social logins are pre-verified
                    FirstName = firstName,
                    LastName = lastName,
                    ImageUrl = imageUrl ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    SecurityStamp = Guid.NewGuid().ToString()
                };

                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    return (false, $"Failed to create user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}", null);
                }

                // Add external login
                var loginInfo = new UserLoginInfo(request.Provider, externalUserId, request.Provider);
                var addLoginResult = await userManager.AddLoginAsync(user, loginInfo);
                if (!addLoginResult.Succeeded)
                {
                    _logger.LogWarning("Failed to add external login, but user was created: {Errors}", 
                        string.Join(", ", addLoginResult.Errors.Select(e => e.Description)));
                }

                // Assign role
                if (role == UserRoles.Business)
                {
                    if (!await roleManager.RoleExistsAsync(UserRoles.Business))
                        await roleManager.CreateAsync(new IdentityRole(UserRoles.Business));
                    await userManager.AddToRoleAsync(user, UserRoles.Business);

                    // Create business for business users
                    var business = new Bussiness
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        Name = $"{firstName} {lastName}'s Business",
                        Description = string.Empty,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        ApprovalStatus = BusinessApprovalStatus.Approved
                    };

                    var businessCreated = await _businessRepository.CreateBusiness(business);
                    if (!businessCreated)
                    {
                        _logger.LogWarning("Failed to create business for social login user");
                    }
                }
                else
                {
                    if (!await roleManager.RoleExistsAsync(UserRoles.User))
                        await roleManager.CreateAsync(new IdentityRole(UserRoles.User));
                    await userManager.AddToRoleAsync(user, UserRoles.User);
                }
            }
            else
            {
                // User exists - check if external login is already linked
                var existingLogins = await userManager.GetLoginsAsync(user);
                var hasExternalLogin = existingLogins.Any(l => l.LoginProvider == request.Provider && l.ProviderKey == externalUserId);

                if (!hasExternalLogin)
                {
                    // Link external login to existing account
                    var loginInfo = new UserLoginInfo(request.Provider, externalUserId, request.Provider);
                    var addLoginResult = await userManager.AddLoginAsync(user, loginInfo);
                    if (!addLoginResult.Succeeded)
                    {
                        _logger.LogWarning("Failed to link external login: {Errors}", 
                            string.Join(", ", addLoginResult.Errors.Select(e => e.Description)));
                    }
                }

                // Update user info if provided
                if (!string.IsNullOrEmpty(firstName) && user.FirstName != firstName)
                    user.FirstName = firstName;
                if (!string.IsNullOrEmpty(lastName) && user.LastName != lastName)
                    user.LastName = lastName;
                if (!string.IsNullOrEmpty(imageUrl) && user.ImageUrl != imageUrl)
                    user.ImageUrl = imageUrl;
                user.UpdatedAt = DateTime.UtcNow;
                await userManager.UpdateAsync(user);
            }

            // Generate JWT token
            var userRoles = await userManager.GetRolesAsync(user);
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            foreach (var userRole in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, userRole));

                if (userRole == UserRoles.Business)
                {
                    var userBusinesses = await _context.Businesses
                        .Where(b => b.UserId == user.Id)
                        .OrderByDescending(b => b.ApprovalStatus == BusinessApprovalStatus.Approved)
                        .ThenBy(b => b.CreatedAt)
                        .ToListAsync();

                    if (userBusinesses.Any())
                    {
                        var businessIds = string.Join(",", userBusinesses.Select(b => b.Id.ToString()));
                        authClaims.Add(new Claim("BusinessIds", businessIds));

                        var primaryBusiness = userBusinesses.FirstOrDefault(b => b.ApprovalStatus == BusinessApprovalStatus.Approved && b.IsActive == true)
                            ?? userBusinesses.FirstOrDefault();

                        if (primaryBusiness != null)
                        {
                            authClaims.Add(new Claim("BusinessId", primaryBusiness.Id.ToString()));
                        }
                    }
                }
            }

            string token = GenerateToken(authClaims);

            var response = new SocialLoginResponse
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ImageUrl = user.ImageUrl,
                Roles = userRoles.ToList(),
                IsNewUser = isNewUser,
                BusinessId = authClaims.FirstOrDefault(c => c.Type == "BusinessId")?.Value,
                BusinessIds = authClaims.FirstOrDefault(c => c.Type == "BusinessIds")?.Value
            };

            return (true, "Social login successful", response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during social login with provider {Provider}", request?.Provider);
            return (false, $"Error during social login: {ex.Message}", null);
        }
    }

    private async Task<(bool Success, string? Email, string? FirstName, string? LastName, string? Picture, string? Subject, string? Id, string Message)> VerifyGoogleTokenAsync(string idToken)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings();
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return (true, payload.Email, payload.GivenName, payload.FamilyName, payload.Picture, payload.Subject, null, "Success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Google token");
            return (false, null, null, null, null, null, null, $"Invalid Google token: {ex.Message}");
        }
    }

    private async Task<(bool Success, string? Email, string? FirstName, string? LastName, string? Picture, string? Subject, string? Id, string Message)> VerifyFacebookTokenAsync(string accessToken)
    {
        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"https://graph.facebook.com/me?fields=id,email,first_name,last_name,picture&access_token={accessToken}");
            
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, null, null, null, null, null, "Invalid Facebook token");
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            var email = root.GetProperty("email").GetString();
            var firstName = root.GetProperty("first_name").GetString();
            var lastName = root.GetProperty("last_name").GetString();
            var id = root.GetProperty("id").GetString();
            string? picture = null;
            
            if (root.TryGetProperty("picture", out var pictureElement) && pictureElement.TryGetProperty("data", out var dataElement))
            {
                picture = dataElement.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
            }

            return (true, email, firstName, lastName, picture, null, id, "Success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Facebook token");
            return (false, null, null, null, null, null, null, $"Invalid Facebook token: {ex.Message}");
        }
    }

    private Task<(bool Success, string? Email, string? FirstName, string? LastName, string? Picture, string? Subject, string? Id, string Message)> VerifyAppleTokenAsync(string idToken)
    {
        try
        {
            // Apple sends JWT tokens - we need to decode and verify
            // For now, we'll decode without full verification (in production, verify signature)
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(idToken);

            var email = jsonToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var subject = jsonToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            var name = jsonToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value;

            string firstName = string.Empty;
            string lastName = string.Empty;

            if (!string.IsNullOrEmpty(name))
            {
                try
                {
                    var nameJson = JsonDocument.Parse(name);
                    firstName = nameJson.RootElement.GetProperty("firstName").GetString() ?? string.Empty;
                    lastName = nameJson.RootElement.GetProperty("lastName").GetString() ?? string.Empty;
                }
                catch
                {
                    // If name is not JSON, use as is
                    firstName = name;
                }
            }

            return Task.FromResult<(bool Success, string? Email, string? FirstName, string? LastName, string? Picture, string? Subject, string? Id, string Message)>((true, email, firstName, lastName, null, subject, null, "Success"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Apple token");
            return Task.FromResult<(bool Success, string? Email, string? FirstName, string? LastName, string? Picture, string? Subject, string? Id, string Message)>((false, null, null, null, null, null, null, $"Invalid Apple token: {ex.Message}"));
        }
    }

    private Task<(bool Success, string? Email, string? FirstName, string? LastName, string? Picture, string? Subject, string? Id, string Message)> VerifyMicrosoftTokenAsync(string idToken)
    {
        try
        {
            // Microsoft sends JWT tokens
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(idToken);

            var email = jsonToken.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == "preferred_username")?.Value;
            var subject = jsonToken.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == "oid")?.Value;
            var name = jsonToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
            var givenName = jsonToken.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value;
            var familyName = jsonToken.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value;
            var picture = jsonToken.Claims.FirstOrDefault(c => c.Type == "picture")?.Value;

            string firstName = givenName ?? string.Empty;
            string lastName = familyName ?? string.Empty;

            if (string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(name))
            {
                var nameParts = name.Split(' ');
                firstName = nameParts[0] ?? string.Empty;
                lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : string.Empty;
            }

            return Task.FromResult<(bool Success, string? Email, string? FirstName, string? LastName, string? Picture, string? Subject, string? Id, string Message)>((true, email, firstName, lastName, picture, subject, null, "Success"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Microsoft token");
            return Task.FromResult<(bool Success, string? Email, string? FirstName, string? LastName, string? Picture, string? Subject, string? Id, string Message)>((false, null, null, null, null, null, null, $"Invalid Microsoft token: {ex.Message}"));
        }
    }

}
