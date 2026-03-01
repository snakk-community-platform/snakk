namespace Snakk.Application.DTOs.Responses;

public record ActiveSessionsResponse(int TotalSessions, IEnumerable<object> Sessions);
