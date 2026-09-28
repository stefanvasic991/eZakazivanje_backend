using System;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Responce;
namespace eZakazivanje.DataService.Repositories.Interfaces;

public interface ICategoryRepository : IGenericRepository<Category>
{
    Task<bool> UpdateCategory(Guid categoryId, Category updatedCategory);
    Task<IEnumerable<CategoryDto>> GetAllCategories();
    Task<bool> AddCategoriesToBusiness(IEnumerable<Guid> categoryIds,Guid businessId);
    Task<bool> AddCategory(Category category);
    Task<bool> UpdateImageAsync(Guid categoryId, string imagePath);
}
