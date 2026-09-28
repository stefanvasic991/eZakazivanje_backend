using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using eZakazivanje.Entity.Settings;

namespace eZakazivanje.Api.Controllers
{
    /// <summary>
    /// Test controller to verify Google Play and Apple configuration settings
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ConfigurationTestController : ControllerBase
    {
        private readonly GooglePlaySettings _googleSettings;
        private readonly AppleSettings _appleSettings;
        private readonly ILogger<ConfigurationTestController> _logger;

        public ConfigurationTestController(
            IOptions<GooglePlaySettings> googleSettings,
            IOptions<AppleSettings> appleSettings,
            ILogger<ConfigurationTestController> logger)
        {
            _googleSettings = googleSettings.Value;
            _appleSettings = appleSettings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Test endpoint to verify Google Play and Apple configuration
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult TestConfiguration()
        {
            try
            {
                var result = new
                {
                    Status = "Configuration Test",
                    Timestamp = DateTime.UtcNow,
                    GooglePlay = new
                    {
                        Configured = !string.IsNullOrEmpty(_googleSettings.PackageName),
                        PackageName = _googleSettings.PackageName,
                        ServiceAccountKeyPath = _googleSettings.ServiceAccountKeyPath,
                        CredentialFileExists = CheckFileExists(_googleSettings.ServiceAccountKeyPath),
                        FullCredentialPath = GetFullPath(_googleSettings.ServiceAccountKeyPath)
                    },
                    Apple = new
                    {
                        Configured = !string.IsNullOrEmpty(_appleSettings.BundleId) &&
                                    !string.IsNullOrEmpty(_appleSettings.KeyId) &&
                                    !string.IsNullOrEmpty(_appleSettings.IssuerId),
                        KeyId = _appleSettings.KeyId,
                        IssuerId = _appleSettings.IssuerId,
                        BundleId = _appleSettings.BundleId,
                        PrivateKeyPath = _appleSettings.PrivateKeyPath,
                        CredentialFileExists = CheckFileExists(_appleSettings.PrivateKeyPath),
                        FullCredentialPath = GetFullPath(_appleSettings.PrivateKeyPath)
                    },
                    Summary = new
                    {
                        AllConfigured = (!string.IsNullOrEmpty(_googleSettings.PackageName) &&
                                        !string.IsNullOrEmpty(_appleSettings.BundleId) &&
                                        !string.IsNullOrEmpty(_appleSettings.KeyId) &&
                                        !string.IsNullOrEmpty(_appleSettings.IssuerId)),
                        AllFilesExist = (CheckFileExists(_googleSettings.ServiceAccountKeyPath) &&
                                       CheckFileExists(_appleSettings.PrivateKeyPath))
                    }
                };

                _logger.LogInformation("Configuration test completed successfully");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing configuration");
                return StatusCode(500, new
                {
                    Status = "Error",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Check if a file exists at the specified path
        /// </summary>
        private bool CheckFileExists(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return false;

            try
            {
                var fullPath = GetFullPath(relativePath);
                return System.IO.File.Exists(fullPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the full path for a relative path
        /// </summary>
        private string GetFullPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return "Not configured";

            try
            {
                return Path.Combine(AppContext.BaseDirectory, relativePath);
            }
            catch
            {
                return relativePath;
            }
        }
    }
}

