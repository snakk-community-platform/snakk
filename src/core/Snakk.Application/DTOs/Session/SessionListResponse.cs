namespace Snakk.Application.DTOs.Session;

public class SessionListResponse
{
    public required List<SessionDto> Sessions { get; set; }
    public int ActiveCount { get; set; }
}
