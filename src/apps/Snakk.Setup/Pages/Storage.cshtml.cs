using Microsoft.AspNetCore.Mvc;

namespace Snakk.Setup.Pages;

public class StorageModel : SetupPageBase
{
    [BindProperty] public string AvatarStoragePath { get; set; } = OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snakk", "storage")
        : "/app/storage";
    [BindProperty] public string StorageProvider { get; set; } = "Local";
    [BindProperty] public string S3Endpoint { get; set; } = "";
    [BindProperty] public string S3AccessKey { get; set; } = "";
    [BindProperty] public string S3SecretKey { get; set; } = "";
    [BindProperty] public string S3BucketName { get; set; } = "";
    [BindProperty] public string S3PublicUrlBase { get; set; } = "";

    public void OnGet()
    {
        ViewData["SetupStep"] = 4;
        var state = GetState();
        AvatarStoragePath = state.AvatarStoragePath;
        StorageProvider = state.StorageProvider;
        S3Endpoint = state.S3Endpoint;
        S3AccessKey = state.S3AccessKey;
        S3SecretKey = state.S3SecretKey;
        S3BucketName = state.S3BucketName;
        S3PublicUrlBase = state.S3PublicUrlBase;
    }

    public IActionResult OnPost()
    {
        ViewData["SetupStep"] = 4;

        // Storage path is always required (used for config file + local fallback)
        if (string.IsNullOrWhiteSpace(AvatarStoragePath))
        {
            ModelState.AddModelError("AvatarStoragePath", "Storage path is required.");
            return Page();
        }

        if (StorageProvider == "S3")
        {
            if (string.IsNullOrWhiteSpace(S3Endpoint))
                ModelState.AddModelError("S3Endpoint", "S3 endpoint is required.");
            if (string.IsNullOrWhiteSpace(S3AccessKey))
                ModelState.AddModelError("S3AccessKey", "Access key is required.");
            if (string.IsNullOrWhiteSpace(S3SecretKey))
                ModelState.AddModelError("S3SecretKey", "Secret key is required.");
            if (string.IsNullOrWhiteSpace(S3BucketName))
                ModelState.AddModelError("S3BucketName", "Bucket name is required.");
            if (string.IsNullOrWhiteSpace(S3PublicUrlBase))
                ModelState.AddModelError("S3PublicUrlBase", "Public URL is required.");

            if (!ModelState.IsValid)
                return Page();
        }

        var state = GetState();
        state.AvatarStoragePath = AvatarStoragePath.Trim();
        state.StorageProvider = StorageProvider;
        state.S3Endpoint = S3Endpoint?.Trim() ?? "";
        state.S3AccessKey = S3AccessKey?.Trim() ?? "";
        state.S3SecretKey = S3SecretKey?.Trim() ?? "";
        state.S3BucketName = S3BucketName?.Trim() ?? "";
        state.S3PublicUrlBase = S3PublicUrlBase?.TrimEnd('/') ?? "";
        SaveState(state);

        return RedirectToPage("AdminAccount");
    }
}
