using Microsoft.AspNetCore.Identity;

namespace CriticalViewer.Core.Entities;

// Extends the built-in Identity user. Add profile fields here as later
// features need them (display name, avatar, etc.) rather than growing
// the Movie/Review entities to carry user display data.
public class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
