using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Data;
using eZakazivanje.Entity.DbSet;

namespace eZakazivanje.DataService.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<NotificationService> _logger;
        private readonly FirebaseMessaging _firebaseMessaging;

        public NotificationService(AppDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
            _firebaseMessaging = FirebaseMessaging.DefaultInstance;
        }

        public async Task SendDirectNotification(Guid userId, string title, string body)
            => await SendNotification(userId, title, body);

        public async Task SendNotificationToBusiness(Guid businessId, string title, string body)
        {
            try
            {
                // Get business first to get UserId
                var business = await _context.Businesses.FindAsync(businessId);
                if (business == null)
                {
                    _logger.LogWarning("Business {BusinessId} not found", businessId);
                    return;
                }

                // Find the business owner using UserId
                var businessOwner = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == business.UserId && 
                                             !string.IsNullOrEmpty(u.FCMToken));

                if (businessOwner == null)
                {
                    _logger.LogWarning("Business owner not found or has no FCM token for business");
                    return;
                }

                if (!Guid.TryParse(business.UserId, out var ownerUserId))
                {
                    _logger.LogWarning("Invalid business owner user id");
                    return;
                }

                await SendNotification(ownerUserId, title, body);

                _logger.LogInformation("Notification sent to business owner for business");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to business");
            }
        }

        private async Task SendNotification(Guid userId, string title, string body)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId.ToString());

                if (user == null || string.IsNullOrEmpty(user.FCMToken))
                {
                    _logger.LogWarning("No FCM token found for user");
                    return;
                }

                var message = new Message
                {
                    Notification = new Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Token = user.FCMToken
                };

                var response = await _firebaseMessaging.SendAsync(message);
                _logger.LogInformation("Notification sent successfully to user");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user");
            }
        }
    }
} 