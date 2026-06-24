namespace Snakk.Admin.Services;

public class AdminThemeService
{
    public bool IsDarkMode { get; private set; } = true;
    public event Action? OnChanged;

    public void Toggle()
    {
        IsDarkMode = !IsDarkMode;
        OnChanged?.Invoke();
    }
}
