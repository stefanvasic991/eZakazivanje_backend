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
    public class EmplyeeController : BaseController
    {
        public EmplyeeController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper)
        {
        }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEmployees()
    {
        var employees = await _unitOfWork.Employees.GetAllEmployeesAsync();
        //var result = _mapper.Map<IEnumerable<EmployeeDto>>(employees);
        return Ok(employees);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEmployee(Guid id)
    {
        var employee = await _unitOfWork.Employees.GetById(id);
        if (employee == null)
            return NotFound($"Employee with id {id} not found");

        //var result = _mapper.Map<EmployeeDto>(employee);
        return Ok(employee);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployee employeeDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var employee = _mapper.Map<Employee>(employeeDto);
        var createdEmployee = await _unitOfWork.Employees.CreateEmployeeAsync(employee);
        if (createdEmployee == null)
            return BadRequest("Nije uspelo kreiranje zaposlenog");

        return CreatedAtAction(nameof(GetEmployee), new { id = createdEmployee.Id }, createdEmployee);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateEmployee(Guid id, [FromBody] CreateEmployee employeeDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existingEmployee = await _unitOfWork.Employees.GetById(id);
        if (existingEmployee == null)
            return NotFound($"Zaposleni sa ID {id} nije pronađen");

        _mapper.Map(employeeDto, existingEmployee);
        var updatedEmployee = await _unitOfWork.Employees.UpdateEmployeeAsync(id, existingEmployee);
        if (!updatedEmployee)
            return BadRequest("Nije uspelo ažuriranje zaposlenog");

        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteEmployee(Guid id)
    {
        var employee = await _unitOfWork.Employees.GetById(id);
        if (employee == null)
            return NotFound($"Zaposleni sa ID {id} nije pronađen");

        var deletedEmployee = await _unitOfWork.Employees.DeleteEmployeeAsync(id);
        if (!deletedEmployee)
            return BadRequest("Nije uspelo brisanje zaposlenog");

        return NoContent();
    }
    }
}
