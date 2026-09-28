using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using eZakazivanje.DataService.Data;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Responce;


namespace eZakazivanje.DataService.Repositories;

public class CategoryRepository : GenericRepository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext context, ILogger logger) : base(context, logger){}

    public async Task<bool> AddCategory(Category category)
    {
        try
        {
            if (category == null)
            {
                _logger.LogWarning("Pokušano dodavanje null kategorije");
                return false;
            }

            await _dbSet.AddAsync(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Kategorija uspešno dodata");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom dodavanja nove kategorije");
            return false;
        }
    }

    public async Task<bool> UpdateCategory(Guid categoryId, Category updatedCategory)
    {
        try
        {
            var existingCategory = await _dbSet.FindAsync(categoryId);

            if (existingCategory == null)
            {
                _logger.LogWarning("Kategorija nije pronađena");
                return false;
            }

            if(!string.IsNullOrEmpty(updatedCategory.Name)){
                existingCategory.Name = updatedCategory.Name;
            }
            if(!string.IsNullOrEmpty(updatedCategory.Description)){
                existingCategory.Description = updatedCategory.Description;
            }
            if(!string.IsNullOrEmpty(updatedCategory.ImageUrl)){
                existingCategory.ImageUrl = updatedCategory.ImageUrl;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Kategorija uspešno ažurirana");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom ažuriranja kategorije");
            return false;
        }
    }

    public async Task<bool> DeleteCategory(Guid categoryId)
    {
        try
        {
            var category = await _dbSet.FindAsync(categoryId);

            if (category == null)
            {
                _logger.LogWarning("Kategorija nije pronađena");
                return false;
            }

            _dbSet.Remove(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Kategorija uspešno obrisana");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom brisanja kategorije");
            return false;
        }
    }

    public async Task<IEnumerable<CategoryDto>> GetAllCategories()
    {
        try
        {
            var categories = await _dbSet
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    ImageUrl = c.ImageUrl,
                    Description = c.Description
                })
                .ToListAsync();
                
            _logger.LogInformation("Pronadjeno {Count} kategorija", categories.Count);
            return categories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom pronalaženja svih kategorija");
            return Enumerable.Empty<CategoryDto>();
        }
    }

    public async Task<Category?> GetCategoryById(Guid categoryId)
    {
        try
        {
            var category = await _dbSet.FindAsync(categoryId);

            if (category == null)
            {
                _logger.LogWarning("Kategorija nije pronađena");
                return null;
            }

            _logger.LogInformation("Pronadjena kategorija");
            return category;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom pronalaženja kategorije");
            return null;
        }
    }

    public async Task<bool> AddCategoriesToBusiness(IEnumerable<Guid> categoryIds, Guid businessId)
    {
        try
        {
            // Get the business with existing categories
            var business = await _context.Set<Bussiness>()
                .Include(b => b.Categories)
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business == null)
            {
                _logger.LogWarning("Biznis entitet sa ID {BusinessId} nije pronađen", businessId);
                return false;
            }

            // Get all categories to be added
            var categoriesToAdd = await _dbSet
                .Where(c => categoryIds.Contains(c.Id))
                .ToListAsync();

            if (!categoriesToAdd.Any())
            {
                _logger.LogWarning("Nije pronađena validna kategorija za navedene ID-je");
                return false;
            }

            // Initialize Categories collection if null
            business.Categories ??= new List<Category>();

            // Add only categories that aren't already associated
            foreach (var category in categoriesToAdd)
            {
                if (!business.Categories.Any(c => c.Id == category.Id))
                {
                    business.Categories.Add(category);
                }
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation(
                "Uspešno dodato {CategoryCount} kategorija biznis entitetu {BusinessId}", 
                categoriesToAdd.Count, 
                businessId);
                
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Greška prilikom dodavanja kategorija biznis entitetu {BusinessId}", 
                businessId);
            return false;
        }
    }

    public async Task<bool> UpdateImageAsync(Guid categoryId, string imagePath)
    {
        try
        {
            var category = await _dbSet.FindAsync(categoryId);
            if (category == null) return false;

            category.ImageUrl = imagePath;
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom ažuriranja slike kategorije");
            return false;
        }
    }

}
