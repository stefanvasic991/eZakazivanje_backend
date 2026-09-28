using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using eZakazivanje.DataService.Data;
using eZakazivanje.Entity.DbSet;
using System.Security.Claims;

namespace eZakazivanje.Api.Authorization
{
    public class ApprovedBusinessAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Skip authorization if action is decorated with [AllowAnonymous] attribute
            if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
                return;

            Guid businessId;
            
            // First, try to get businessId from query parameter
            var businessIdQuery = context.HttpContext.Request.Query["businessId"].FirstOrDefault();
            if (!string.IsNullOrEmpty(businessIdQuery) && Guid.TryParse(businessIdQuery, out businessId))
            {
                // Use businessId from query parameter
            }
            else
            {
                // Fall back to business ID from the claims (primary business)
                var businessIdClaim = context.HttpContext.User.FindFirst("BusinessId");
                if (businessIdClaim == null || !Guid.TryParse(businessIdClaim.Value, out businessId))
                {
                    context.Result = new ObjectResult("Nepostoji preduzeća povezano sa ovim nalogom")
                    {
                        StatusCode = 403
                    };
                    return;
                }
            }

            // Get the DbContext
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

            // Get user ID from claims
            var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                context.Result = new ObjectResult("User ID not found in token")
                {
                    StatusCode = 403
                };
                return;
            }

            // Check if the business exists and user owns it
            var business = dbContext.Businesses.Find(businessId);
            if (business == null)
            {
                context.Result = new NotFoundResult();
                return;
            }

            // Verify user owns this business
            if (business.UserId != userIdClaim.Value)
            {
                context.Result = new ObjectResult("Nemate pristup ovom biznisu")
                {
                    StatusCode = 403
                };
                return;
            }

            if (business.ApprovalStatus != BusinessApprovalStatus.Approved || !business.IsActive.GetValueOrDefault())
            {
                context.Result = new ObjectResult("Vaš biznis još nije odobren ili nije aktivan")
                {
                    StatusCode = 403
                };
                return;
            }

            // Check if the user's email is confirmed
            var user = dbContext.Users.Find(userIdClaim.Value);
            if (user == null || !user.EmailConfirmed)
            {
                context.Result = new ObjectResult("Molimo potvrdite vašu email adresu pre nastavka")
                {
                    StatusCode = 403
                };
                return;
            }
        }
    }
} 