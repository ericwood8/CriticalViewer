namespace CriticalViewer.Api.Contracts;

public record RegisterRequest(string Email, string Password);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record AuthResponse(string Token, DateTimeOffset ExpiresAt, string Email);
