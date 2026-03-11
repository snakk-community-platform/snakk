namespace Snakk.Admin.Services;

/// <summary>
/// Scoped service that stores JWT tokens for the lifetime of a Blazor Server circuit.
/// The tokens are captured from cookies during the initial HTTP request (_Host.cshtml)
/// and passed to App.razor, which stores them here.
/// The GrpcAuthInterceptor reads and updates these tokens for auto-refresh.
/// </summary>
public class CircuitTokenProvider
{
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
}
