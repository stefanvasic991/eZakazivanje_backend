using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using eZakazivanje.DataService.Repositories.Interfaces;
using eZakazivanje.Entity.DbSet;
using eZakazivanje.Entity.DTOS.Request;

namespace eZakazivanje.Api.Controllers
{
    /// <summary>
    /// Controller for managing user addresses
    /// Provides CRUD operations for user address information
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Requires authentication for all endpoints
    public class AddressController : BaseController
    {
        private readonly ILogger<AddressController> _logger;
        
        /// <summary>
        /// Constructor for AddressController
        /// </summary>
        /// <param name="unitOfWork">Unit of work for data access</param>
        /// <param name="logger">Logger for error tracking</param>
        /// <param name="mapper">AutoMapper for object mapping</param>
        public AddressController(IUnitOfWork unitOfWork, ILogger<AddressController> logger, IMapper mapper)
            : base(unitOfWork, mapper)
        {
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the address for the currently authenticated user
        /// </summary>
        /// <returns>
        /// 200 OK: User's address found and returned
        /// 401 Unauthorized: User not authenticated
        /// 404 Not Found: No address found for user
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserAddress()
        {
            try
            {
                // Extract user ID from JWT token claims
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                // Get all addresses and find the one belonging to the current user
                var addresses = await _unitOfWork.Addresses.GetAllAddresses();
                var userAddress = addresses.FirstOrDefault(a => a.ApplicationUserId == userId);
                
                // Return 404 if no address is found for the user
                if (userAddress == null)
                {
                    return NotFound("Nije pronadjen nijedna adresa za ovog korisnika");
                }

                // Map and return the user's address
                return Ok(_mapper.Map<Address>(userAddress));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom dobijanja adrese korisnika");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        /// <summary>
        /// Creates a new address for the currently authenticated user
        /// </summary>
        /// <param name="address">Address data to create</param>
        /// <returns>
        /// 201 Created: Address successfully created
        /// 400 Bad Request: Invalid model state
        /// 401 Unauthorized: User not authenticated
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateAddress([FromBody] CreateAddress address)
        {
            // Validate the incoming model
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Extract user ID from JWT token claims
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                // Map the DTO to entity and set required properties
                var addressEntity = _mapper.Map<Address>(address);
                addressEntity.Id = Guid.NewGuid(); // Generate new GUID for address
                addressEntity.ApplicationUserId = userId; // Set the foreign key to user ID

                // Save the address to database
                var result = await _unitOfWork.Addresses.AddAddress(addressEntity);
                return CreatedAtAction(nameof(GetUserAddress), null, _mapper.Map<Address>(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom kreiranja adrese");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        /// <summary>
        /// Updates the address for the currently authenticated user
        /// </summary>
        /// <param name="updatedAddress">Updated address data</param>
        /// <returns>
        /// 200 OK: Address successfully updated
        /// 400 Bad Request: Invalid model state
        /// 401 Unauthorized: User not authenticated
        /// 404 Not Found: No address found for user or update failed
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpPut]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateAddress([FromBody] CreateAddress updatedAddress)
        {
            // Validate the incoming model
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Extract user ID from JWT token claims
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                // Get the existing address for this user
                var addresses = await _unitOfWork.Addresses.GetAllAddresses();
                var existingAddress = addresses.FirstOrDefault(a => a.ApplicationUserId == userId);
                
                // Return 404 if no address exists for the user
                if (existingAddress == null)
                {
                    return NotFound("Nije pronadjena nijedna adresa za ovog korisnika");
                }

                // Map the updated data to entity while preserving the original ID and user relationship
                var addressEntity = _mapper.Map<Address>(updatedAddress);
                addressEntity.Id = existingAddress.Id;  // Keep the same address ID
                addressEntity.ApplicationUserId = userId;  // Keep the same user ID

                // Update the address in database
                var result = await _unitOfWork.Addresses.UpdateAddress(existingAddress.Id, addressEntity);
                if (result == null)
                {
                    return NotFound("Nije uspelo ažuriranje adrese");
                }

                return Ok("Adresa uspešno ažurirana.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom ažuriranja adrese");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }

        /// <summary>
        /// Deletes the address for the currently authenticated user
        /// </summary>
        /// <returns>
        /// 200 OK: Address successfully deleted
        /// 401 Unauthorized: User not authenticated
        /// 404 Not Found: No address found for user or deletion failed
        /// 500 Internal Server Error: Server error occurred
        /// </returns>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteAddress()
        {
            try
            {
                // Extract user ID from JWT token claims
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                // Get the existing address for this user
                var addresses = await _unitOfWork.Addresses.GetAllAddresses();
                var existingAddress = addresses.FirstOrDefault(a => a.ApplicationUserId == userId);
                
                // Return 404 if no address exists for the user
                if (existingAddress == null)
                {
                    return NotFound("Nije pronadjena nijedna adresa za ovog korisnika");
                }

                // Delete the address from database
                var result = await _unitOfWork.Addresses.DeleteAddress(existingAddress.Id);
                if (!result)
                {
                    return NotFound("Nije uspelo brisanje adrese");
                }

                return Ok("Adresa uspešno obrisana.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greska prilikom brisanja adrese");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    "Došlo je do greške prilikom obrade vašeg zahteva.");
            }
        }
    }
}
