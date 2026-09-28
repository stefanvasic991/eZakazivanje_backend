using System;

namespace eZakazivanje.Entity.DbSet;

public class Comment : BaseEntity
{
    public string? Content { get; set; }
    public int? Rating { get; set; }
    public DateTime? CommentDate { get; set; }

    // Navigation properties
    public string? ApplicationUserId { get; set; }
    public virtual ApplicationUser? ApplicationUser { get; set; }

    public Guid? BusinessId { get; set; }
    public Bussiness?Business { get; set; }
}
