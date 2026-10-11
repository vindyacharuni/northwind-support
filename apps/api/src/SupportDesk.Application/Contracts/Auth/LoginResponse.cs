namespace SupportDesk.Application.Contracts.Auth;

public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, CurrentUserDto Agent);
