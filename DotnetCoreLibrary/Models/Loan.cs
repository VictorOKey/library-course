namespace DotnetCoreLibrary.Models
{
    public class Loan
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int BookId { get; set; }
        public DateTime DateIssued { get; set; }
        public DateTime? DateReturned { get; set; }
    }
}
