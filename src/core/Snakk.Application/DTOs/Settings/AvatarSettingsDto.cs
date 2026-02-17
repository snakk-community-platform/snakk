namespace Snakk.Application.DTOs.Settings;

public class AvatarSettingsDto
{
    public int DefaultAvatarSize { get; set; } = 80;
    public int MaxUploadSizeMb { get; set; } = 5;
    public bool AllowUploads { get; set; } = true;
    public bool GenerateOnStartup { get; set; } = false;
}
