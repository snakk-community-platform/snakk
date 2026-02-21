namespace Snakk.AdminWeb.Services;

/// <summary>
/// Scoped service that stores the JWT token for the lifetime of a Blazor Server circuit.
/// The token is captured from the .Snakk.Auth cookie during the initial HTTP request
/// (_Host.cshtml) and passed to App.razor, which stores it here.
/// </summary>
public class CircuitTokenProvider
{
    public string? Token { get; set; }
}
