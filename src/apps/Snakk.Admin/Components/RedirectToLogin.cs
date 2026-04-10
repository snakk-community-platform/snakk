using Microsoft.AspNetCore.Components;

namespace Snakk.Admin.Components;

public class RedirectToLogin : ComponentBase
{
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized()
    {
        Navigation.NavigateTo($"Auth/Login?returnUrl={Uri.EscapeDataString(Navigation.Uri)}", true);
    }
}
