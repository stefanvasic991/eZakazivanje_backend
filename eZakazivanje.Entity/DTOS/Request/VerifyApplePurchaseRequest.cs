namespace eZakazivanje.Entity.DTOS.Request;

public class VerifyApplePurchaseRequest
{
    public required string TransactionId { get; set; }
    public required string ProductId { get; set; }
    public required List<Guid> BusinessIds { get; set; } // Changed to support multiple businesses
    public string? OriginalTransactionId { get; set; }
}

