namespace MockTest.DTOs;

public class GetBorrowingDetailsDto
{
    public int Id { get; set; }
    public DateTime BorrowDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public List<GetBooksDetailsDto> Books { get; set; } = [];
}