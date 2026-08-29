namespace CriticalViewer.Core.Entities;

public class Review
{
    public Guid Id { get; set; }
    public Guid MovieId { get; set; }
    public Movie? Movie { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    // Brief calls for a five-star rating; enforce 1-5 at the API/validation
    // layer and again with a CHECK constraint in the DbContext config.
    // Stored as tinyint (see AppDbContext) to match dbo.Reviews.Rating.
    public int Rating { get; set; }
    public required string Body { get; set; }

    // dbo.Reviews.CreatedAt is DATETIME2(0), not DATETIMEOFFSET - always UTC.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
