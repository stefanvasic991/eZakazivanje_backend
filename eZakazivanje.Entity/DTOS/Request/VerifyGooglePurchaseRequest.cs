namespace eZakazivanje.Entity.DTOS.Request;

public class VerifyGooglePurchaseRequest
{
    public required string PurchaseToken { get; set; }
    public required string ProductId { get; set; }
    public required List<Guid> BusinessIds { get; set; } // Changed to support multiple businesses
    public string? OrderId { get; set; }
}

