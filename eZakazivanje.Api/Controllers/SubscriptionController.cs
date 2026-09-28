using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.IO;
using eZakazivanje.Api.Authorization;
using eZakazivanje.DataService.Services.Interfaces;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DTOS.Request;
using eZakazivanje.Entity.DTOS.Response;
using eZakazivanje.Entity.DbSet;
using AutoMapper;

namespace eZakazivanje.Api.Controllers
{
    /// <summary>
    /// Controller for managing business subscriptions (Google Play and Apple App Store)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = UserRoles.Business)]
    [ApprovedBusiness]
    public class SubscriptionController : BaseController
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(
            ISubscriptionService subscriptionService,
            ILogger<SubscriptionController> logger,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        /// <summary>
        /// Verify and create/update Google Play subscriptions for multiple businesses
        /// </summary>
        [HttpPost("verify-google")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> VerifyGooglePurchase([FromBody] VerifyGooglePurchaseRequest request)
        {
            try
            {
                // Get user ID from token
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Forbid("User ID not found in token");
                }

                // Validate request
                if (request.BusinessIds == null || !request.BusinessIds.Any())
                {
                    return BadRequest(new { message = "At least one business ID is required" });
                }

                var (success, message, subscriptions) = await _subscriptionService.VerifyGooglePurchaseAsync(request, userId);

                if (!success)
                {
                    return BadRequest(new { message });
                }

                return Ok(new { message, subscriptions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying Google Play purchase");
                return StatusCode(500, new { message = "An error occurred while verifying the purchase" });
            }
        }

        /// <summary>
        /// Verify and create/update Apple App Store subscriptions for multiple businesses
        /// </summary>
        [HttpPost("verify-apple")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> VerifyApplePurchase([FromBody] VerifyApplePurchaseRequest request)
        {
            try
            {
                // Get user ID from token
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Apple verification - User ID not found in JWT token");
                    return Forbid("User ID not found in token");
                }

                _logger.LogInformation("Apple verification - User ID from token: {UserId}, Requested BusinessIds: {BusinessIds}", 
                    userId, request.BusinessIds != null ? string.Join(", ", request.BusinessIds) : "null");

                // Validate request
                if (string.IsNullOrWhiteSpace(request.TransactionId))
                {
                    return BadRequest(new { message = "TransactionId is required" });
                }

                if (string.IsNullOrWhiteSpace(request.ProductId))
                {
                    return BadRequest(new { message = "ProductId is required" });
                }

                if (request.BusinessIds == null || !request.BusinessIds.Any())
                {
                    return BadRequest(new { message = "At least one business ID is required" });
                }

                var (success, message, subscriptions) = await _subscriptionService.VerifyApplePurchaseAsync(request, userId);

                if (!success)
                {
                    _logger.LogWarning("Apple purchase verification failed: {Message}. TransactionId: {TransactionId}, ProductId: {ProductId}, BusinessIds: {BusinessIds}", 
                        message, request.TransactionId, request.ProductId, string.Join(", ", request.BusinessIds));
                    return BadRequest(new { message });
                }

                return Ok(new { message, subscriptions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying Apple purchase");
                return StatusCode(500, new { message = "An error occurred while verifying the purchase" });
            }
        }

        /// <summary>
        /// Get the active subscription for the current business
        /// </summary>
        [HttpGet("active")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetActiveSubscription([FromQuery] Guid? businessId = null)
        {
            try
            {
                var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
                if (!success)
                {
                    return Forbid(errorMessage ?? "Access denied");
                }

                var subscription = await _subscriptionService.GetActiveSubscriptionAsync(targetBusinessId);

                if (subscription == null)
                {
                    return NotFound(new { message = "No active subscription found" });
                }

                return Ok(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active subscription");
                return StatusCode(500, new { message = "An error occurred while retrieving the subscription" });
            }
        }

        /// <summary>
        /// Get all subscriptions for the current business
        /// </summary>
        [HttpGet("all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBusinessSubscriptions([FromQuery] Guid? businessId = null)
        {
            try
            {
                var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
                if (!success)
                {
                    return Forbid(errorMessage ?? "Access denied");
                }

                var subscriptions = await _subscriptionService.GetBusinessSubscriptionsAsync(targetBusinessId);
                return Ok(subscriptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting business subscriptions");
                return StatusCode(500, new { message = "An error occurred while retrieving subscriptions" });
            }
        }

        /// <summary>
        /// Get all available subscription plans
        /// </summary>
        [HttpGet("plans")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAvailablePlans()
        {
            try
            {
                var plans = await _subscriptionService.GetAvailablePlansAsync();
                return Ok(plans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available plans");
                return StatusCode(500, new { message = "An error occurred while retrieving subscription plans" });
            }
        }

        /// <summary>
        /// Cancel a subscription
        /// </summary>
        [HttpPost("{subscriptionId}/cancel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CancelSubscription(Guid subscriptionId, [FromQuery] Guid? businessId = null)
        {
            try
            {
                var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
                if (!success)
                {
                    return Forbid(errorMessage ?? "Access denied");
                }

                var successResult = await _subscriptionService.CancelSubscriptionAsync(subscriptionId, targetBusinessId);

                if (!successResult)
                {
                    return NotFound(new { message = "Subscription not found or does not belong to this business" });
                }

                return Ok(new { message = "Subscription cancelled successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling subscription");
                return StatusCode(500, new { message = "An error occurred while cancelling the subscription" });
            }
        }

        /// <summary>
        /// Webhook endpoint for Google Play subscription notifications
        /// </summary>
        [HttpPost("webhook/google")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GooglePlayWebhook([FromBody] GooglePlayWebhookRequest? request)
        {
            try
            {
                // Validate request
                if (request == null)
                {
                    _logger.LogWarning("Google Play webhook: Received null request");
                    return BadRequest(new { message = "Request body is required" });
                }

                // In production, verify the webhook signature from Google
                // For now, we'll process the notification directly

                var success = await _subscriptionService.HandleGooglePlayWebhookAsync(request);

                if (success)
                {
                    return Ok(new { message = "Webhook processed successfully" });
                }

                return BadRequest(new { message = "Failed to process webhook" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Google Play webhook: {Error}", ex.Message);
                return StatusCode(500, new { message = "An error occurred while processing the webhook" });
            }
        }

        /// <summary>
        /// Webhook endpoint for Apple App Store subscription notifications
        /// </summary>
        [HttpPost("webhook/apple")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AppleWebhook()
        {
            try
            {
                // Enable buffering to allow reading the request body
                Request.EnableBuffering();
                Request.Body.Position = 0;
                
                // Read raw request body to handle Apple's signed JWT payload format
                using var reader = new StreamReader(Request.Body, leaveOpen: true);
                var requestBody = await reader.ReadToEndAsync();
                
                // Reset position for potential future reads
                Request.Body.Position = 0;
                
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    _logger.LogWarning("Apple webhook: Received empty request body");
                    return BadRequest(new { message = "Request body is required" });
                }

                AppleWebhookRequest? request = null;

                try
                {
                    // Try to deserialize as JSON (handles both signedPayload and decoded Payload formats)
                    request = System.Text.Json.JsonSerializer.Deserialize<AppleWebhookRequest>(
                        requestBody,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Apple webhook: Failed to deserialize request body. Body: {Body}", 
                        requestBody.Length > 500 ? requestBody.Substring(0, 500) + "..." : requestBody);
                    return BadRequest(new { message = "Invalid request format" });
                }

                if (request == null)
                {
                    _logger.LogWarning("Apple webhook: Deserialized request is null");
                    return BadRequest(new { message = "Request body is required" });
                }

                // In production, verify and decode the signed payload from Apple
                // For now, we'll handle the decoded payload directly

                var success = await _subscriptionService.HandleAppleWebhookAsync(request);

                if (success)
                {
                    return Ok(new { message = "Webhook processed successfully" });
                }

                return BadRequest(new { message = "Failed to process webhook" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Apple webhook: {Error}", ex.Message);
                return StatusCode(500, new { message = "An error occurred while processing the webhook" });
            }
        }
    }
}

