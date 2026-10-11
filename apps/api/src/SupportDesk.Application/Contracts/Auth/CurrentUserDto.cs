namespace SupportDesk.Application.Contracts.Auth;

public sealed record CurrentUserDto(int Id, string FullName, string Email);
