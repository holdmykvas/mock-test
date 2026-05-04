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

        //OPEN the connection
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@StudentId", studentId);

        await using var reader = await command.ExecuteReaderAsync();

        GetStudentDetailsDto results = null;

        //Optional: saving time from looping
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
                //Creating base level Student.Further comes -> Borrowing -> Book
                results = new GetStudentDetailsDto()
                {
                    FirstName = reader.GetString(ordFirstName),
                    LastName = reader.GetString(ordLastName),
                    Borrowing = new List<GetBorrowingDetailsDto>()
                };
            }     
            
            //Check if we added this borrowing somewhere
            var borrowId = reader.GetInt32(ordBorrowingId);
            var borrowing = results.Borrowing.FirstOrDefault(e => e.Id.Equals(borrowId));

            //if borrowing is new
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
        //If results is empty -> query didn't run since line 45
        return results ?? throw new NotFoundException($"Student with ID {studentId} not found.");
    }

    //POST
    public async Task CreateBorrowingAsync(int studentId, CreateBorrowingBooksDto dto)
    {
        //2 DATA Insertion
        var CreateBorrowingQuery = """
                                    INSERT INTO Borrowing (StudentId, BorrowDate, ReturnDate)
                                    VALUES (@StudentId, @BorrowDate, @ReturnDate)
                                    SELECT @@IDENTITY;
                                   """;

        var CreateBorrowingBookQuery = """
                                        INSERT INTO Borrowing_Book
                                       VALUES (@BorrowId, @BookId)
                                       """;

        //2 DATA Validation
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
        
        // We open the connection and explicitly start a TRANSACTION
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
            
            //INSERTING CHILDREN 
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
            //VERY IMPORTANT!!!!!!!  |
            //DONT FORGET!!!!!!     \/
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    //PUT
    public async Task ReturnBorrowingAsync(int borrowingId)
    {
        // We need one query to ask a question (SELECT) and one to take action (UPDATE).
        var checkStatusQuery = "SELECT ReturnDate FROM Borrowing WHERE Id = @BorrowingId";
        var updateQuery = "UPDATE Borrowing SET ReturnDate = @ReturnDate WHERE Id = @BorrowingId";

        //OPEN CONNECTION
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        //We are looking for return date so -> .ExecuteScalarAsync();
        await using var checkCommand = new SqlCommand(checkStatusQuery, connection);
        checkCommand.Parameters.AddWithValue("@BorrowingId", borrowingId);
        
        var returnDateResult = await checkCommand.ExecuteScalarAsync();

        //If null -> doesn't exist in DB
        if (returnDateResult == null)
        {
            throw new NotFoundException($"Borrowing with ID {borrowingId} not found.");
        }
        
        //If return name exists -> it was already returned - no need for more
        if (returnDateResult != DBNull.Value)
        {
            throw new BadRequestException($"Borrowing with ID {borrowingId} has already been returned.");
        }
        
        //Now execute UPDATE
        await using var updateCommand = new SqlCommand(updateQuery, connection);
        
        updateCommand.Parameters.AddWithValue("@ReturnDate", DateTime.Now); 
        updateCommand.Parameters.AddWithValue("@BorrowingId", borrowingId);
        
        var rowsAffected = await updateCommand.ExecuteNonQueryAsync();
        
        //Everything worked but nothing has changed
        if (rowsAffected == 0)
        {
            throw new Exception("Failed to update the borrowing record.");
        }
    }
    
    public async Task DeleteBorrowingAsync(int borrowingId)
    {
        //IMPORTANT ORDER: CHECK -> DELETE CHILD -> DELETE PARENT
        var checkQuery = "SELECT 1 FROM Borrowing WHERE Id = @BorrowingId";
        
        var deleteBooksQuery = "DELETE FROM Borrowing_Book WHERE BorrowingId = @BorrowingId";
        
        var deleteBorrowingQuery = "DELETE FROM Borrowing WHERE Id = @BorrowingId";

        //OPEN CONNECTION
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        await using var checkCommand = new SqlCommand(checkQuery, connection);
        checkCommand.Parameters.AddWithValue("@BorrowingId", borrowingId);
        
        //VALIDATE
        var exists = await checkCommand.ExecuteScalarAsync();
        if (exists == null)
        {
            throw new NotFoundException($"Borrowing with ID {borrowingId} not found.");
        }
        
        // Because we are modifying two separate tables, we MUST group them into an ACID transaction
        await using var transaction = await connection.BeginTransactionAsync();
        
        await using var deleteCommand = new SqlCommand();
        deleteCommand.Connection = connection;
        deleteCommand.Transaction = transaction as SqlTransaction;

        try
        {
            
            //// We must sever the Foreign Key ties before we can touch the main record.
            deleteCommand.CommandText = deleteBooksQuery;
            deleteCommand.Parameters.AddWithValue("@BorrowingId", borrowingId);
            await deleteCommand.ExecuteNonQueryAsync();
            
            //Now that the children are gone, the parent is safe to delete.
            deleteCommand.Parameters.Clear();
            deleteCommand.CommandText = deleteBorrowingQuery;
            deleteCommand.Parameters.AddWithValue("@BorrowingId", borrowingId);
            await deleteCommand.ExecuteNonQueryAsync();
            
            //If both deletions were successful, we permanently commit the changes
            await transaction.CommitAsync();
        }
        catch (Exception){
            
            await transaction.RollbackAsync();
            throw;
        }
    }
}