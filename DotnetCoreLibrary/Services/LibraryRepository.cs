using DotnetCoreLibrary.Models;
using System.Collections.Generic;

namespace DotnetCoreLibrary.Services
{
    public class LibraryRepository
    {
        public List<Book> Books { get; } = new();
        public List<User> Users { get; } = new();
        public List<Loan> Loans { get; } = new();
    }
}