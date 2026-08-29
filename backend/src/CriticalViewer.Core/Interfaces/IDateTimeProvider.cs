namespace CriticalViewer.Core.Interfaces;

// Thin seam so tests can control "now" instead of calling DateTimeOffset.UtcNow
// directly inside services - keeps review timestamps and token expiry testable.
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
