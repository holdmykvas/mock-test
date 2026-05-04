using MockTest.DTOs;

namespace MockTest.Services;

public interface IDbService
{
    Task<GetStudentDetailsDto> GetStudentDetailsAsync(int studentId);
}