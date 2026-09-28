using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DTOS.Request;

namespace eZakazivanje.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImageController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ImageController> _logger;

        public ImageController(
            IFileService fileService,
            IUnitOfWork unitOfWork,
            ILogger<ImageController> logger)
        {
            _fileService = fileService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        [HttpPost("upload")]
        [Authorize]
        [RequestSizeLimit(5 * 1024 * 1024)] // 5MB limit
        public async Task<IActionResult> UploadImage([FromForm] ImageUploadRequest request)
        {
            try
            {
                if (!_fileService.IsValidImage(request.ImageFile))
                {
                    return BadRequest("Nevalidna slika. Samo .jpg, .jpeg, i .png fajlovi do 5MB su dozvoljeni.");
                }

                var imagePath = await _fileService.UploadImageAsync(
                    request.ImageFile, 
                    request.EntityType, 
                    request.EntityId
                );

                // Update entity with new image path
                var success = await UpdateEntityImagePath(request.EntityType, request.EntityId, imagePath);
                if (!success)
                {
                    return BadRequest($"Nije uspelo ažuriranje slike za {request.EntityType}");
                }

                return Ok(new { imagePath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom uploadovanja slike");
                return StatusCode(500, "Došlo je do greške prilikom uploadovanja slike");
            }
        }

        private async Task<bool> UpdateEntityImagePath(string entityType, Guid entityId, string imagePath)
        {
            switch (entityType.ToLower())
            {
                case "user":
                    return await _unitOfWork.Users.UpdateImageAsync(entityId, imagePath);
                case "category":
                    return await _unitOfWork.Categories.UpdateImageAsync(entityId, imagePath);
                case "service":
                    return await _unitOfWork.Services.UpdateImageAsync(entityId, imagePath);
                case "employee":
                    return await _unitOfWork.Employees.UpdateImageAsync(entityId, imagePath);
                default:
                    return false;
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetImage([FromQuery] string imagePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    return BadRequest("Putanja slike je obavezna");
                }

                var imageResult = await _fileService.GetImageAsync(imagePath);
                if (imageResult == null)
                {
                    return NotFound("Slika nije pronađena");
                }
                return File(imageResult.Value.FileContents, imageResult.Value.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom pronalaženja slike");
                return StatusCode(500, "Došlo je do greške prilikom pronalaženja slike");
            }
        }
    }
} 