using Microsoft.AspNetCore.Connections.Features;
using Microsoft.Data.SqlClient;
using MockTest.DTOs;

namespace MockTest.Services;

public class DbService : IDbService
{
    
    private readonly string _connectionString;

    public DbService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }
    public async Task<GetStudentDetailsDto> GetStudentDetailsAsync(int studentId)
    {
        var query = """
                    SELECT 
                    s.FirstName AS FirstName,
                    s.LastName AS LastName,
                    bo.Id AS BorrowingId,
                    bo.BorrowDate AS BorrowDate,
                    bo.ReturnDate AS ReturnDate,
                    b.Title as Title,
                    b.Author as Author
                    FROM Student s
                    JOIN Borrowing bo ON bo.StudentId = s.Id
                    JOIN Borrowing_Book bb ON bb.BorrowingId = bo.Id
                    JOIN Book b  ON b.Id = bb.BookId
                    WHERE s.Id = @StudentId 
                    """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("StudentId", studentId);

        await using var reader = await command.ExecuteReaderAsync();

        GetStudentDetailsDto results = null;

        var ordFirstName = reader.GetOrdinal("FirstName");
        var ordLastName = reader.GetOrdinal("LastName");
        var ordBorrowingId = reader.GetOrdinal("BorrowingId");
        var ordBorrowDate = reader.GetOrdinal("BorrowDate");
        var ordReturnDate = reader.GetOrdinal("ReturnDate");
        var ordBookTitle = reader.GetOrdinal("Title");
        var ordBookAuthor = reader.GetOrdinal("Author");

        while (await reader.ReadAsync()) 
        {
            if (results == null)
            {
                results = new GetStudentDetailsDto()
                {
                    FirstName = reader.GetString(ordFirstName),
                    LastName = reader.GetString(ordLastName),
                    Borrowing = new List<GetBorrowingDetailsDto>()
                };
            }     
            
            var borrowId = reader.GetInt32(ordBorrowingId);
            var borrowing = results.Borrowing.FirstOrDefault(e => e.Id.Equals(borrowId));

            if (borrowing is null)
            {
                borrowing = new GetBorrowingDetailsDto()
                {
                    Id = borrowId,
                    BorrowDate = reader.GetDateTime(ordBorrowDate),
                    ReturnDate = reader.IsDBNull(ordReturnDate) ? null : reader.GetDateTime(ordReturnDate),
                    Books = new List<GetBooksDetailsDto>()
                };
                results.Borrowing.Add(borrowing);
            }
            
            borrowing.Books.Add(new GetBooksDetailsDto()
            {
                Title = reader.GetString(ordBookTitle),
                Author = reader.GetString(ordBookAuthor)
            });
        }
        return results ?? throw new Exception("No results found for this id");
    }
}