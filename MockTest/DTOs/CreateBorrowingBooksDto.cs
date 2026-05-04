namespace MockTest.DTOs;

public class CreateBorrowingBooksDto
{
    public  DateTime BorrowDate { get; set; }
    public List<CreateBooksDetailsDto> Books { get; set; } = [];
}