namespace CriticalViewer.Core.Entities;

public class Movie
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Genre { get; set; }
    public required string Director { get; set; }
    public int ReleaseYear { get; set; }
    public string? PosterUrl { get; set; }
    public string? Tagline { get; set; }
    public required string Summary { get; set; }

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
