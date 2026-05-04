using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MockTest.DTOs;
using MockTest.Exceptions;
using MockTest.Services;

namespace MockTest.Controllers;

[Route("api/students")]
[ApiController]
public class StudentController : ControllerBase
{
    private readonly IDbService _dbService;

    public StudentController(IDbService dbService)
    {
        _dbService = dbService;
    }

    [Route("{id}/borrowings")]
    [HttpGet]
    public async Task<IActionResult> GetStudentBorrowing(int id)
    {
        try
        {
            var result = await _dbService.GetStudentDetailsAsync(id);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [Route("{id}/borrowings")]
    [HttpPost]
    public async Task<IActionResult> Post([FromRoute] int id, [FromBody] CreateBorrowingBooksDto dto)
    {
        if (!dto.Books.Any())
        {
            return BadRequest("At least 1 item is required");
        }

        try
        {
            await _dbService.CreateBorrowingAsync(id, dto);
            return Created($"api/students/{id}/borrowings", dto);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

}