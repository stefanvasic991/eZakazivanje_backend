using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Request;
using eZakazivanje.Entity.DTOS.Response;

namespace eZakazivanje.Api.Controllers;

/// <summary>
/// Admin controller for managing subscription plans
/// </summary>
[Route("api/admin/subscription-plans")]
[ApiController]
[Authorize(Roles = "Admin")]
public class SubscriptionPlanAdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<SubscriptionPlanAdminController> _logger;

    public SubscriptionPlanAdminController(
        AppDbContext context,
        ILogger<SubscriptionPlanAdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all subscription plans
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPlans()
    {
        try
        {
            var plans = await _context.SubscriptionPlans
                .OrderBy(p => p.Platform)
                .ThenBy(p => p.Price)
                .ToListAsync();

            var response = plans.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                description = p.Description,
                price = p.Price,
                durationDays = p.DurationDays,
                platform = p.Platform,
                productId = p.ProductId,
                isActive = p.IsActive,
                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt,
                subscriptionCount = p.Subscriptions?.Count ?? 0
            }).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription plans");
            return StatusCode(500, new { message = "An error occurred while retrieving subscription plans" });
        }
    }

    /// <summary>
    /// Get a specific subscription plan by ID
    /// </summary>
    [HttpGet("{planId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanById(Guid planId)
    {
        try
        {
            var plan = await _context.SubscriptionPlans
                .Include(p => p.Subscriptions)
                .FirstOrDefaultAsync(p => p.Id == planId);

            if (plan == null)
            {
                return NotFound(new { message = "Subscription plan not found" });
            }

            var response = new
            {
                id = plan.Id,
                name = plan.Name,
                description = plan.Description,
                price = plan.Price,
                durationDays = plan.DurationDays,
                platform = plan.Platform,
                productId = plan.ProductId,
                isActive = plan.IsActive,
                createdAt = plan.CreatedAt,
                updatedAt = plan.UpdatedAt,
                subscriptionCount = plan.Subscriptions?.Count ?? 0
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription plan {PlanId}", planId);
            return StatusCode(500, new { message = "An error occurred while retrieving the subscription plan" });
        }
    }

    /// <summary>
    /// Create a new subscription plan
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePlan([FromBody] CreateSubscriptionPlanRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if plan with same ProductId and Platform already exists
            var existingPlan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(p => 
                    p.ProductId == request.ProductId && 
                    p.Platform == request.Platform);

            if (existingPlan != null)
            {
                return BadRequest(new 
                { 
                    message = $"A subscription plan with ProductId '{request.ProductId}' and Platform '{request.Platform}' already exists" 
                });
            }

            var plan = new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description ?? string.Empty,
                Price = request.Price,
                DurationDays = request.DurationDays,
                Platform = request.Platform,
                ProductId = request.ProductId,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.SubscriptionPlans.AddAsync(plan);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Subscription plan created: {PlanId}, {ProductId}, {Platform}", 
                plan.Id, plan.ProductId, plan.Platform);

            var response = new
            {
                id = plan.Id,
                name = plan.Name,
                description = plan.Description,
                price = plan.Price,
                durationDays = plan.DurationDays,
                platform = plan.Platform,
                productId = plan.ProductId,
                isActive = plan.IsActive,
                createdAt = plan.CreatedAt,
                updatedAt = plan.UpdatedAt
            };

            return CreatedAtAction(nameof(GetPlanById), new { planId = plan.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription plan");
            return StatusCode(500, new { message = "An error occurred while creating the subscription plan" });
        }
    }

    /// <summary>
    /// Update an existing subscription plan
    /// </summary>
    [HttpPut("{planId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePlan(Guid planId, [FromBody] UpdateSubscriptionPlanRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var plan = await _context.SubscriptionPlans.FindAsync(planId);
            if (plan == null)
            {
                return NotFound(new { message = "Subscription plan not found" });
            }

            // Check if another plan with same ProductId and Platform exists (excluding current plan)
            var duplicatePlan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(p => 
                    p.ProductId == request.ProductId && 
                    p.Platform == request.Platform &&
                    p.Id != planId);

            if (duplicatePlan != null)
            {
                return BadRequest(new 
                { 
                    message = $"Another subscription plan with ProductId '{request.ProductId}' and Platform '{request.Platform}' already exists" 
                });
            }

            // Update plan properties
            plan.Name = request.Name;
            plan.Description = request.Description ?? string.Empty;
            plan.Price = request.Price;
            plan.DurationDays = request.DurationDays;
            plan.Platform = request.Platform;
            plan.ProductId = request.ProductId;
            plan.IsActive = request.IsActive;
            plan.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Subscription plan updated: {PlanId}", planId);

            var response = new
            {
                id = plan.Id,
                name = plan.Name,
                description = plan.Description,
                price = plan.Price,
                durationDays = plan.DurationDays,
                platform = plan.Platform,
                productId = plan.ProductId,
                isActive = plan.IsActive,
                createdAt = plan.CreatedAt,
                updatedAt = plan.UpdatedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subscription plan {PlanId}", planId);
            return StatusCode(500, new { message = "An error occurred while updating the subscription plan" });
        }
    }

    /// <summary>
    /// Delete a subscription plan (soft delete by setting IsActive = false)
    /// </summary>
    [HttpDelete("{planId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePlan(Guid planId)
    {
        try
        {
            var plan = await _context.SubscriptionPlans
                .Include(p => p.Subscriptions)
                .FirstOrDefaultAsync(p => p.Id == planId);

            if (plan == null)
            {
                return NotFound(new { message = "Subscription plan not found" });
            }

            // Check if plan has active subscriptions
            var activeSubscriptions = plan.Subscriptions?.Any(s => s.IsActive && !s.IsCancelled) ?? false;
            if (activeSubscriptions)
            {
                return BadRequest(new 
                { 
                    message = "Cannot delete subscription plan with active subscriptions. Deactivate it instead." 
                });
            }

            // Soft delete by setting IsActive = false
            plan.IsActive = false;
            plan.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Subscription plan deactivated: {PlanId}", planId);

            return Ok(new { message = "Subscription plan deactivated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subscription plan {PlanId}", planId);
            return StatusCode(500, new { message = "An error occurred while deleting the subscription plan" });
        }
    }

    /// <summary>
    /// Toggle plan active status
    /// </summary>
    [HttpPatch("{planId}/toggle-active")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TogglePlanActive(Guid planId, [FromBody] bool isActive)
    {
        try
        {
            var plan = await _context.SubscriptionPlans.FindAsync(planId);
            if (plan == null)
            {
                return NotFound(new { message = "Subscription plan not found" });
            }

            plan.IsActive = isActive;
            plan.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Subscription plan {PlanId} active status changed to {IsActive}", planId, isActive);

            return Ok(new 
            { 
                message = $"Subscription plan active status updated to {isActive}",
                isActive = plan.IsActive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling subscription plan active status {PlanId}", planId);
            return StatusCode(500, new { message = "An error occurred while updating the subscription plan" });
        }
    }
}

