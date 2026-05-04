using Microsoft.AspNetCore.Connections.Features;
using Microsoft.Data.SqlClient;
using MockTest.DTOs;
using MockTest.Exceptions;

namespace MockTest.Services;

public class DbService : IDbService
{
    
    private readonly string _connectionString;

    public DbService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }
    //GET
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
        command.Parameters.AddWithValue("@StudentId", studentId);

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
        return results ?? throw new NotFoundException($"Student with ID {studentId} not found.");
    }

    public async Task CreateBorrowingAsync(int studentId, CreateBorrowingBooksDto dto)
    {
        var CreateBorrowingQuery = """
                                    INSERT INTO Borrowing (StudentId, BorrowDate, ReturnDate)
                                    VALUES (@StudentId, @BorrowDate, @ReturnDate)
                                    SELECT @@IDENTITY;
                                   """;

        var CreateBorrowingBookQuery = """
                                        INSERT INTO BorrowingBook
                                       VALUES (@BorrowId, @BookId)
                                       """;

        var checkStudentQuery = """
                                SELECT 1
                                FROM Student
                                WHERE Id = @StudentId
                                """;

        var checkBookQuery = """
                              SELECT 1
                              FROM Book
                              WHERE Id = @BookId
                             """;
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var transaction = await connection.BeginTransactionAsync();
        
        await using var command = new SqlCommand();
        command.Connection = connection;
        command.Transaction = transaction as SqlTransaction;

        try
        {
            //CHECKING IF STUDENT EXIST
            command.Parameters.Clear();
            command.CommandText = checkStudentQuery;
            command.Parameters.AddWithValue("@StudentId", studentId);

            var studentIdResult = await command.ExecuteScalarAsync();

            if (studentIdResult == null)
            {
                throw new NotFoundException($"Student with ID {studentId} not found.");
            }

            //ADDING Borrowing
            command.Parameters.Clear();
            command.CommandText = CreateBorrowingQuery;
            command.Parameters.AddWithValue("@BorrowDate", dto.BorrowDate);
            command.Parameters.AddWithValue("@ReturnDate", DBNull.Value);
            command.Parameters.AddWithValue("@StudentId", studentId);

            var borrowingObj = await command.ExecuteScalarAsync();
            var borrowingId = Convert.ToInt32(borrowingObj);

            foreach (var borrowingBook in dto.Books)
            {
                //CHECKING IF BOOK EXISTS
                command.Parameters.Clear();
                command.CommandText = checkBookQuery;
                command.Parameters.AddWithValue("@BookId", borrowingBook.BookId);
                
                var bookExists = await command.ExecuteScalarAsync();
                if (bookExists == null)
                {
                    throw new NotFoundException($"Book with ID {borrowingBook.BookId} not found.");
                }
                
                //ADDING BOOK TO BORROWING
                command.Parameters.Clear();
                command.CommandText = CreateBorrowingBookQuery;
                command.Parameters.AddWithValue("@BorrowId", borrowingId);
                command.Parameters.AddWithValue("@BookId", borrowingBook.BookId);
                
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}