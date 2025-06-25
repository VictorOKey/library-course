using DotnetCoreLibrary.Models;
using DotnetCoreLibrary.Services;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();

var repo = new LibraryRepository();
var httpClient = new HttpClient();

//вывод книги
app.MapGet("/books", () => repo.Books);
//создание по автору и названию
app.MapPost("/books", (Book book) =>
{
    if (string.IsNullOrWhiteSpace(book.Title) || string.IsNullOrWhiteSpace(book.Author))
        return Results.BadRequest("Название и Автор обязательны!");

    book.Id = repo.Books.Count + 1;
    book.IsAvailable = true;
    repo.Books.Add(book);
    return Results.Created($"/books/{book.Id}", book);
});
//юзереы
app.MapGet("/users", () => repo.Users);
//создание юзеров
app.MapPost("/users", (User user) =>
{
    if (string.IsNullOrWhiteSpace(user.Email))
        return Results.BadRequest("Email обязателен!");
    if (!user.Email.Contains('@') || !user.Email.Contains('.'))
        return Results.BadRequest("Email некорректен!");
    if (user.BirthDate == default)
        return Results.BadRequest("Дата рождения обязательна!");

    user.Id = repo.Users.Count + 1;
    repo.Users.Add(user);
    return Results.Created($"/users/{user.Id}", user);
});
//бронь на книгах по id юзера и id книги
app.MapPost("/loans", async (int bookId, int userId) =>
{
    var book = repo.Books.FirstOrDefault(b => b.Id == bookId);
    var user = repo.Users.FirstOrDefault(u => u.Id == userId);

    if (book == null || user == null)
        return Results.BadRequest("Книга или пользователь не найдены");

    if (!book.IsAvailable)
        return Results.BadRequest("Книга недоступна");

    if (book.AgeLimit.HasValue)
    {
        int userAge = GetUserAge(user.BirthDate);
        if (userAge < book.AgeLimit.Value)
            return Results.BadRequest($"Книга доступна с {book.AgeLimit}+");
    }

    var loan = new Loan
    {
        Id = repo.Loans.Count + 1,
        BookId = bookId,
        UserId = userId,
        DateIssued = DateTime.UtcNow,
        DateReturned = null
    };

    book.IsAvailable = false;
    repo.Loans.Add(loan);

    await UpdateStatsAsync(httpClient, repo);

    return Results.Created($"/loans/{loan.Id}", loan);
});
//возврат книг по id брони
app.MapPost("/returns", async (int loanId) =>
{
    var loan = repo.Loans.FirstOrDefault(l => l.Id == loanId && l.DateReturned == null);
    if (loan == null)
        return Results.BadRequest("Бронь не найдена");

    loan.DateReturned = DateTime.UtcNow;

    var book = repo.Books.FirstOrDefault(b => b.Id == loan.BookId);
    if (book != null)
        book.IsAvailable = true;

    await UpdateStatsAsync(httpClient, repo);

    return Results.Ok(loan);
});
//проверка, занята ли книга щас
app.MapGet("/books/{id}/current-holder", (int id) =>
{
    var loan = repo.Loans.FirstOrDefault(l => l.BookId == id && l.DateReturned == null);
    if (loan == null) return Results.NotFound("Книга сейчас никем не занята");
    var user = repo.Users.FirstOrDefault(u => u.Id == loan.UserId);
    if (user == null) return Results.NotFound("Пользователь не найден");
    return Results.Ok(user);
});
//сколько у юзера книг на руках и какие
app.MapGet("/users/{id}/current-books", (int id) =>
{
    var loanedBooks = repo.Loans
        .Where(l => l.UserId == id && l.DateReturned == null)
        .Select(l => repo.Books.FirstOrDefault(b => b.Id == l.BookId))
        .Where(b => b != null)
        .ToList();
    return Results.Ok(loanedBooks);
});
//история книги, её бронь
app.MapGet("/books/{id}/history", (int id) =>
{
    var history = repo.Loans
        .Where(l => l.BookId == id)
        .Select(l => new {
            User = repo.Users.FirstOrDefault(u => u.Id == l.UserId)?.Name ?? "Unknown",
            DateIssued = l.DateIssued,
            DateReturned = l.DateReturned
        })
        .ToList();

    if (!history.Any()) return Results.NotFound("Эта книга никогда не выдавалась");
    return Results.Ok(history);
});
//история юзера
app.MapGet("/users/{id}/history", (int id) =>
{
    var history = repo.Loans
        .Where(l => l.UserId == id)
        .Select(l => new {
            Book = repo.Books.FirstOrDefault(b => b.Id == l.BookId)?.Title ?? "Unknown",
            DateIssued = l.DateIssued,
            DateReturned = l.DateReturned
        })
        .ToList();

    if (!history.Any()) return Results.NotFound("Пользователь никогда не брал книг");
    return Results.Ok(history);
});
//к вспомогательному сервису "поиск" по названию книги или автору
app.MapGet("/search", async (string? title, string? author) =>
{
    var query = new List<string>();
    if (!string.IsNullOrWhiteSpace(title))
        query.Add($"title={Uri.EscapeDataString(title)}");
    if (!string.IsNullOrWhiteSpace(author))
        query.Add($"author={Uri.EscapeDataString(author)}");
    var url = "http://go-search-service:8080/books/search";
    if (query.Count > 0)
        url += "?" + string.Join("&", query);

    try
    {
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return Results.StatusCode((int)response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        return Results.Content(content, "application/json");
    }
    catch
    {
        return Results.Problem("Go-сервис поиска недоступен", statusCode: 502);
    }
});
//к вспомогательному сервису "статистика" сколько книг всего, сколько в броне, сколько свободных
app.MapGet("/stats", async () =>
{
    try
    {
        var response = await httpClient.GetAsync("http://go-stats-service:8081/stats");
        if (!response.IsSuccessStatusCode)
            return Results.StatusCode((int)response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        return Results.Content(content, "application/json");
    }
    catch
    {
        return Results.Problem("Go-сервис статистики недоступен", statusCode: 502);
    }
});
//к вспомогательному сервису "рекомендации" сортировка книг по возрасту пользователя
app.MapGet("/recommend", async (int userId) =>
{
    var url = $"http://go-recommend-service:8082/recommend?userId={userId}";
    try
    {
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return Results.StatusCode((int)response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        return Results.Content(content, "application/json");
    }
    catch
    {
        return Results.Problem("Сервис рекомендаций недоступен", statusCode: 502);
    }
});

app.Run();
//для статистики передача данных
static async Task UpdateStatsAsync(HttpClient httpClient, LibraryRepository repo)
{
    var totalBooks = repo.Books.Count;
    var booksOnLoan = repo.Loans.Count(l => l.DateReturned == null);
    var booksAvailable = repo.Books.Count(b => b.IsAvailable);

    var stats = new
    {
        totalBooks = totalBooks,
        booksOnLoan = booksOnLoan,
        booksAvailable = booksAvailable
    };

    try
    {
        await httpClient.PostAsJsonAsync("http://go-stats-service:8081/stats/update", stats);
    }
    catch { }
}
static int GetUserAge(DateTime birthDate)
{
    var today = DateTime.Today;
    var age = today.Year - birthDate.Year;
    if (birthDate.Date > today.AddYears(-age)) age--;
    return age;
}