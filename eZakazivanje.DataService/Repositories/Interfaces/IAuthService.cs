using System;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Request;
using eZakazivanje.Entity.DTOS.Response;


namespace eZakazivanje.DataService.Repositories.Interfaces;

public interface IAuthService
{
    Task<(int status, string message, string? userId)> Registration(RegistraionModel model, string role);
    Task<(int, string)> Login(LoginModel model);
    Task<(bool Success, string Message, SocialLoginResponse? Response)> SocialLoginAsync(SocialLoginRequest request);
    Task<IEnumerable<string>> GetUserRoles(string userId);
    Task SendVerificationEmail(ApplicationUser user);
    Task<IEnumerable<ApplicationUserResponse>> GetAllUsers();
}
