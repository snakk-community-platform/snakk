using Snakk.Application.DTOs.Management;

namespace Snakk.Admin.Services;

/// <summary>
/// Scoped service that bridges ManagePage (which resolves scope) and ManageLayout (which needs it for sidebar).
/// CascadingValue flows DOWN only, so this service allows ManagePage to share scope UP to the layout.
/// </summary>
public class ManageScopeState
{
    public ManageScopeDto? Scope { get; private set; }

    public event Action? OnChange;

    public void SetScope(ManageScopeDto? scope)
    {
        Scope = scope;
        OnChange?.Invoke();
    }
}
