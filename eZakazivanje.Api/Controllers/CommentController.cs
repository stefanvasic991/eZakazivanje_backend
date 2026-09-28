using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Request;

namespace eZakazivanje.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentController : BaseController
    {
        public CommentController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper)
        {
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllComments()
        {
            try
            {
                var comments = await _unitOfWork.Comments.GetAllComments();
                return Ok(comments);
            }
            catch (Exception)
            {
                return StatusCode(500, "Došlo je do greške prilikom pronalaženja komentara");
            }
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCommentById(Guid id)
        {
            try
            {
                var comment = await _unitOfWork.Comments.GetCommentById(id);
                if (comment == null)
                {
                    return NotFound($"Komentar sa ID {id} nije pronađen");
                }
                return Ok(comment);
            }
            catch (Exception)
            {
                return StatusCode(500, "Došlo je do greške prilikom pronalaženja komentara");
            }
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateComment([FromBody] CreateComment commentDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var comment = _mapper.Map<Comment>(commentDto);
                var createdComment = await _unitOfWork.Comments.CreateComment(comment);
                if (createdComment == null)
                {
                    return BadRequest("Nije uspelo kreiranje komentara");
                }

                return CreatedAtAction(nameof(GetCommentById), new { id = createdComment.Id }, createdComment);
            }
            catch (Exception)
            {
                return StatusCode(500, "Došlo je do greške prilikom kreiranja komentara");
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateComment(Guid id, [FromBody] CreateComment commentDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var comment = _mapper.Map<Comment>(commentDto);
                var result = await _unitOfWork.Comments.UpdateComment(id, comment);
                if (!result)
                {
                    return NotFound($"Komentar sa ID {id} nije pronađen");
                }

                return Ok("Komentar je uspešno ažuriran");
            }
            catch (Exception)
            {
                return StatusCode(500, "Došlo je do greške prilikom ažuriranja komentara");
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteComment(Guid id)
        {
            try
            {
                var result = await _unitOfWork.Comments.DeleteComment(id);
                if (!result)
                {
                    return NotFound($"Komentar sa ID {id} nije pronađen");
                }

                return Ok("Komentar je uspešno obrisan");
            }
            catch (Exception)
            {
                return StatusCode(500, "Došlo je do greške prilikom brisanja komentara");
            }
        }
    }
}
