namespace TrackerForSites.Api.Models.Dtos;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string FullName,
    string Email
);

public record RefreshRequest(string RefreshToken);
