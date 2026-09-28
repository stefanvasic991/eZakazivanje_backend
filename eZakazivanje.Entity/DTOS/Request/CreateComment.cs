using System;

namespace eZakazivanje.Entity.DTOS.Request;

public class CreateComment
{
    public string? Content { get; set; }
    public int Rating { get; set; }
    public DateTime CommentDate { get; set; }
}
