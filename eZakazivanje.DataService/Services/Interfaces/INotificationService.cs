using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Response;

namespace eZakazivanje.DataService.Services;

public interface INotificationService
{
    Task SendDirectNotification(Guid userId, string title, string body);
    Task SendNotificationToBusiness(Guid businessId, string title, string body);
} 