using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eZakazivanje.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShareController : ControllerBase
    {
        private readonly ILogger<ShareController> _logger;

        public ShareController(ILogger<ShareController> logger)
        {
            _logger = logger;
        }

        [HttpGet("business/{businessId}")]
        [AllowAnonymous] // Allow access without authentication
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> ShareBusiness(int businessId)
        {
            try
            {
                // Return HTML page with deep linking to specific business
                var html = GenerateBusinessSharePage(businessId);
                return Task.FromResult<IActionResult>(Content(html, "text/html"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating business share page for business");
                return Task.FromResult<IActionResult>(StatusCode(500, "Error loading shared business content"));
            }
        }

        private string GenerateBusinessSharePage(int businessId)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>eZakazivanje - Business Details</title>
    
    <!-- Open Graph Meta Tags for rich previews when sharing -->
    <meta property='og:title' content='eZakazivanje - Book Appointments with This Business'>
    <meta property='og:description' content='Discover this business and book appointments easily. Download eZakazivanje app now!'>
    <meta property='og:type' content='website'>
    <meta property='og:url' content='{Request.Scheme}://{Request.Host}/api/Share/business/{businessId}'>
    
    <script>
        // Deep link to specific business in the app
        const businessAppUrl = 'ezakazivanje://business/{businessId}';
        const playStoreUrl = 'https://play.google.com/store/apps/details?id=com.easyswitch.ezakazivanje';
        const appStoreUrl = 'https://apps.apple.com/app/idcom.softikos.eZakazivanje';
        
        // Try to open the app with business details immediately
        window.location.href = businessAppUrl;
        
        // Detect platform and redirect accordingly
        const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent);
        const isAndroid = /Android/.test(navigator.userAgent);
        
        // If app doesn't open, redirect to appropriate store after 1.5 seconds
        setTimeout(() => {{
            if (isIOS) {{
                window.location.href = appStoreUrl;
            }} else if (isAndroid) {{
                window.location.href = playStoreUrl;
            }} else {{
                // Default to Play Store for other platforms
                window.location.href = playStoreUrl;
            }}
        }}, 1500);
        
        // No tracking needed
    </script>
</head>
<body>
    <!-- Minimal content - user won't see this as they'll be redirected immediately -->
    <div style='text-align: center; padding: 20px; font-family: Arial, sans-serif;'>
        <p>Redirecting to business details in eZakazivanje app...</p>
    </div>
</body>
</html>";
        }
    }
}
