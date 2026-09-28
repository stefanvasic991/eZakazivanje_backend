using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;    

namespace eZakazivanje.DataService.Repositories;

public class CommentRepository : GenericRepository<Comment>, ICommentRepository
{
    public CommentRepository(AppDbContext context, ILogger logger) : base(context, logger){}

    public async Task<IEnumerable<Comment>> GetAllComments()
    {
        try
        {
            var comments = await _dbSet.ToListAsync();
            _logger.LogInformation("Pronadjeno {Count} komentara", comments.Count);
            return comments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom pronalaženja svih komentara");
            return Enumerable.Empty<Comment>();
        }
    }

    public async Task<Comment?> GetCommentById(Guid commentId)
    {
        try
        {
            var comment = await _dbSet.FindAsync(commentId);

            if (comment == null)
            {   
                _logger.LogWarning("Komentar sa ID {CommentId} nije pronađen", commentId);
                return null;
            }

            _logger.LogInformation("Pronadjen komentar {CommentId}", commentId);
            return comment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom pronalaženja komentara {CommentId}", commentId);
            return null;
        }
    }

    public async Task<Comment?> CreateComment(Comment comment)
    {
        try
        {
            var result = await _dbSet.AddAsync(comment);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Kreiran novi komentar sa ID {CommentId}", comment.Id);
            return result.Entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom kreiranja komentara");
            return null;
        }
    }

    public async Task<bool> UpdateComment(Guid commentId, Comment comment)
    {
        try
        {
            var existingComment = await _dbSet.FindAsync(commentId);
            
            if (existingComment == null)
            {
                _logger.LogWarning("Komentar sa ID {CommentId} nije pronađen za ažuriranje", commentId);
                return false;
            }

            existingComment.Content = comment.Content ?? existingComment.Content;
            existingComment.Rating = comment.Rating ?? existingComment.Rating;
            existingComment.ApplicationUserId = comment.ApplicationUserId ?? existingComment.ApplicationUserId;
            existingComment.BusinessId = comment.BusinessId ?? existingComment.BusinessId;
            existingComment.UpdatedAt = DateTime.UtcNow;

            _dbSet.Update(existingComment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ažuriran komentar {CommentId}", commentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom ažuriranja komentara {CommentId}", commentId);
            return false;
        }
    }

    public async Task<bool> DeleteComment(Guid commentId)
    {
        try
        {
            var comment = await _dbSet.FindAsync(commentId);

            if (comment == null)
            {
                _logger.LogWarning("Komentar sa ID {CommentId} nije pronađen za brisanje", commentId);
                return false;
            }

            _dbSet.Remove(comment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Obrisan komentar {CommentId}", commentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom brisanja komentara {CommentId}", commentId);
            return false;
        }
    }

}
