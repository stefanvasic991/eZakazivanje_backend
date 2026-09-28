using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Request;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace eZakazivanje.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoryController : BaseController
    {

    private readonly ILogger<CategoryController> _logger;
 

    public CategoryController(IUnitOfWork unitOfWork, ILogger<CategoryController> logger, IMapper mapper) : base(unitOfWork, mapper)
    {
        
        _logger = logger;
    
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllCategories()
    {
        try
        {
            var categories = await _unitOfWork.Categories.GetAllCategories();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja kategorija");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCategoryById(Guid id)
    {
        try
        {
            var category = await _unitOfWork.Categories.GetById(id);

            if (category == null)
            {
                return NotFound($"Kategorija sa ID {id} nije pronađena.");
            }

            return Ok(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja kategorije {CategoryId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategory createCategoryDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var category = _mapper.Map<Category>(createCategoryDto);
            var result = await _unitOfWork.Categories.AddCategory(category);

            if (!result)
            {
                return BadRequest("Nije uspelo kreiranje kategorije.");
            }

            return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id }, category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom kreiranja nove kategorije");
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CreateCategory createCategoryDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var category = _mapper.Map<Category>(createCategoryDto);
            var result = await _unitOfWork.Categories.UpdateCategory(id, category);

            if (!result)
            {
                return NotFound($"Kategorija sa ID {id} nije pronađena.");
            }

            return Ok($"Kategorija sa ID {id} je uspešno ažurirana.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom ažuriranja kategorije {CategoryId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        try
        {
            var result = await _unitOfWork.Categories.Delete(id);

            if (!result)
            {
                return NotFound($"Kategorija sa ID {id} nije pronađena.");
            }

            return Ok($"Kategorija sa ID {id} je uspešno obrisana.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom brisanja kategorije {CategoryId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpPost("businesses/categories")]
    [Authorize(Roles = UserRoles.Business)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddCategoriesToBusiness([FromBody] IEnumerable<Guid> categoryIds, [FromQuery] Guid? businessId = null)
    {
        if (!categoryIds.Any())
        {
            return BadRequest("Morate proslediti najmanje jedan ID kategorije");
        }

        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage);
            }

            var result = await _unitOfWork.Categories.AddCategoriesToBusiness(categoryIds, targetBusinessId);

            if (!result)
            {
                return NotFound("Jedna ili više kategorija nije pronađena");
            }

            await _unitOfWork.CompleteAsync();
            return Ok("Kategorije su uspešno dodate vašem preduzeću");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom dodavanja kategorija preduzeću");
            return StatusCode(
                StatusCodes.Status500InternalServerError, 
                "Došlo je do greške prilikom obrade vašeg zahteva.");
        }
    }

    [HttpDelete("businesses/categories")]
    [Authorize(Roles = UserRoles.Business)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveCategoriesFromBusiness([FromBody] IEnumerable<Guid> categoryIdsToKeep, [FromQuery] Guid? businessId = null)
    {
        try
        {
            var (success, targetBusinessId, errorMessage) = await GetAndValidateBusinessIdAsync(businessId);
            if (!success)
            {
                return Forbid(errorMessage);
            }

            var result = await _unitOfWork.Bussinesses.RemoveBusinessCategories(targetBusinessId, categoryIdsToKeep);
            
            if (!result)
            {
                return NotFound("Vaše preduzeće nema kategorije za brisanje");
            }

            return Ok($"Kategorije preduzeća su uspešno ažurirane. Ostale {categoryIdsToKeep.Count()} kategorije.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Došlo je do greške prilikom brisanja kategorija preduzeća");
            return StatusCode(500, "Došlo je do greške prilikom ažuriranja kategorija preduzeća");
        }
    }
}
}

