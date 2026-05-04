using MockTest.DTOs;

namespace MockTest.Services;

public interface IDbService
{
    Task<GetStudentDetailsDto> GetStudentDetailsAsync(int studentId);
    
    Task CreateBorrowingAsync(int studentId, CreateBorrowingBooksDto createBorrowingBooksDto);

    Task ReturnBorrowingAsync(int borrowingId);
    
    Task DeleteBorrowingAsync(int borrowingId);
}