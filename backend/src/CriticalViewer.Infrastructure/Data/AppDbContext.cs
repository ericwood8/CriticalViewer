using CriticalViewer.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CriticalViewer.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Movie>(entity =>
        {
            // Column names match dbo.Movies exactly (MovieId, not Id).
            entity.Property(m => m.Id).HasColumnName("MovieId");
            entity.Property(m => m.Title).HasMaxLength(300).IsRequired();
            entity.Property(m => m.Genre).HasMaxLength(100).IsRequired();
            entity.Property(m => m.Director).HasMaxLength(200).IsRequired();
            entity.Property(m => m.PosterUrl).HasMaxLength(500);
            entity.Property(m => m.Tagline).HasMaxLength(300);
            entity.Property(m => m.Summary).HasMaxLength(2000).IsRequired();

            // Supports the search/filter feature: title, genre, director, year.
            entity.HasIndex(m => m.Title);
            entity.HasIndex(m => m.Genre);
            entity.HasIndex(m => m.Director);
            entity.HasIndex(m => m.ReleaseYear);
        });

        builder.Entity<Review>(entity =>
        {
            // Column names match dbo.Reviews exactly (ReviewId, not Id).
            entity.Property(r => r.Id).HasColumnName("ReviewId");
            entity.Property(r => r.Rating).HasColumnType("tinyint");
            entity.Property(r => r.Body).HasMaxLength(2000).IsRequired();
            entity.Property(r => r.CreatedAt)
                  .HasColumnType("datetime")
                  .HasDefaultValueSql("UTC_TIMESTAMP()");

            entity.ToTable(t => t.HasCheckConstraint("CK_Reviews_Rating", "`Rating` BETWEEN 1 AND 5"));

            entity.HasOne(r => r.Movie)
                  .WithMany(m => m.Reviews)
                  .HasForeignKey(r => r.MovieId)
                  .HasConstraintName("FK_Reviews_Movies")
                  .OnDelete(DeleteBehavior.Cascade);

            // dbo.Reviews' reviewer-reference column is named ReviewerId
            // physically (see CriticalViewerDB.sql) - it holds AspNetUsers.Id
            // values from the real app, not dbo.Reviewers.ReviewerId values,
            // so there's no DB-level FK for it (see that file's FK note).
            entity.Property(r => r.UserId).HasColumnName("ReviewerId");

            entity.HasOne(r => r.User)
                  .WithMany()
                  .HasForeignKey(r => r.UserId)
                  .HasConstraintName("FK_Reviews_Users")
                  .OnDelete(DeleteBehavior.Restrict);

            // One review per user per movie, per dbo.Reviews' UQ_Reviews_Movie_User.
            entity.HasIndex(r => new { r.MovieId, r.UserId })
                  .IsUnique()
                  .HasDatabaseName("UQ_Reviews_Movie_User");

            // Supports the movie detail view's infinite-scroll review list
            // (newest or paged reads filtered by MovieId). MySQL/InnoDB has
            // no equivalent to SQL Server's covering-index INCLUDE syntax -
            // a plain composite index is the closest match, and InnoDB
            // secondary indexes implicitly carry the primary key anyway.
            entity.HasIndex(r => new { r.MovieId, r.CreatedAt });
        });
    }
}
