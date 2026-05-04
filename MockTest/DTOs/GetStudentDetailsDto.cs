namespace MockTest.DTOs;

public class GetStudentDetailsDto
{
    public string FirstName { get; set; }= string.Empty;
    public string LastName { get; set; }= string.Empty;
    public List<GetBorrowingDetailsDto> Borrowing { get; set; } = [];
}