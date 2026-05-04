using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MockTest.Exceptions;
using MockTest.Services;

namespace MockTest.Controllers;

[Route("api/students")]
[ApiController]
public class StudentController
{
    private readonly IDbService _dbService;
    public StudentController(IDbService dbService)
    {
        _dbService =  dbService;
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
        catch (NotFoundException ex) //TODO NotFoundException
        {
            return NotFound(ex.Message);  //TODO NotFound
        }
    }
}