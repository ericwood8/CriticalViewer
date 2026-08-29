namespace CriticalViewer.Api.Contracts;

public record ReviewListItem(
    Guid Id,
    string Username,
    int Rating,
    string Body,
    DateTime CreatedAt);

public record CreateReviewRequest(int Rating, string Body);
