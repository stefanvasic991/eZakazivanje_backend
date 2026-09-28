using System;
using eZakazivanje.Entity.DbSet;

namespace eZakazivanje.DataService.Repositories.Interfaces;

public interface ICommentRepository : IGenericRepository<Comment>
{
    Task<bool> DeleteComment(Guid commentId);
    Task<bool> UpdateComment(Guid commentId, Comment comment);
    Task<Comment?> GetCommentById(Guid commentId);
    Task<Comment?> CreateComment(Comment comment);
    //Task<IEnumerable<Comment>> GetCommentsByUserId(G
    Task<IEnumerable<Comment>> GetAllComments();
}
