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
    //Route: GET api/students/{id}/borrowings
    [Route("{id}/borrowings")]
    [HttpGet]
    public async Task<IActionResult> GetStudentBorrowing(int id)
    {
        try
        {
            //asking service for data
            var result = await _dbService.GetStudentDetailsAsync(id);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            //giving 404 instead of default exception
            return NotFound(ex.Message);
        }
    }

    [Route("{id}/borrowings")]
    [HttpPost]
    public async Task<IActionResult> Post([FromRoute] int id, [FromBody] CreateBorrowingBooksDto dto)
    {
        // Before bother the database, check if the user sent a valid request.
        if (!dto.Books.Any())
        {
            return BadRequest("At least 1 item is required");
        }

        try
        {
            //POST - 201 (CREATED)
            await _dbService.CreateBorrowingAsync(id, dto);
            return Created($"api/students/{id}/borrowings", dto);
        }
        catch (NotFoundException ex)
        {
            //Student or Book don't exist
            return NotFound(ex.Message);
        }
    }

    [Route("{borrowingId}/return")]
    [HttpPut]
    public async Task<IActionResult> ReturnBorrowing(int borrowingId)
    {
        try
        {
            await _dbService.ReturnBorrowingAsync(borrowingId);
            
            // 200 OK or 204 No Content are both standard for successful PUT requests
            return Ok($"Borrowing {borrowingId} successfully returned."); 
        }
        catch (NotFoundException ex)
        {
            //If borrowing doesn't exist
            return NotFound(ex.Message); // Returns 404
        }
        catch (BadRequestException ex)
        {
            //The borrowing was already returned
            return BadRequest(ex.Message); // Returns 400
        }
    }
    
// This overrides the controller's base route ("api/students") because borrowings are an independent resource here
    [Route("/api/borrowings/{borrowingId}")]
    [HttpDelete]
    public async Task<IActionResult> DeleteBorrowing(int borrowingId)
    {
        try
        {
            await _dbService.DeleteBorrowingAsync(borrowingId);
            
            // Translates to 204 (No Content)
            return NoContent(); 
        }
        catch (NotFoundException ex)
        {
            //No borrowing found 
            return NotFound(ex.Message);
        }
    }
    
}