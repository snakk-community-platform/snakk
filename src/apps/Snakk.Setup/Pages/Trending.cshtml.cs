using Microsoft.AspNetCore.Mvc;

namespace Snakk.Setup.Pages;

public class TrendingModel : SetupPageBase
{
    [BindProperty] public bool AdaptiveVolumeWindowEnabled { get; set; } = true;
    [BindProperty] public int TrendingTargetPosts { get; set; } = 500;
    [BindProperty] public int TrendingTargetDiscussions { get; set; } = 100;
    [BindProperty] public int TrendingLookbackHours { get; set; } = 24;
    [BindProperty] public int SpacesLookbackDays { get; set; } = 7;
    [BindProperty] public int ContributorsLookbackDays { get; set; } = 7;

    public void OnGet()
    {
        ViewData["SetupStep"] = 11;
        var state = GetState();
        AdaptiveVolumeWindowEnabled = state.AdaptiveVolumeWindowEnabled;
        TrendingTargetPosts = state.TrendingTargetPosts;
        TrendingTargetDiscussions = state.TrendingTargetDiscussions;
        TrendingLookbackHours = state.TrendingLookbackHours;
        SpacesLookbackDays = state.SpacesLookbackDays;
        ContributorsLookbackDays = state.ContributorsLookbackDays;
    }

    public IActionResult OnPost()
    {
        var state = GetState();
        state.AdaptiveVolumeWindowEnabled = AdaptiveVolumeWindowEnabled;
        state.TrendingTargetPosts = Math.Max(50, TrendingTargetPosts);
        state.TrendingTargetDiscussions = Math.Max(10, TrendingTargetDiscussions);
        state.TrendingLookbackHours = Math.Max(1, TrendingLookbackHours);
        state.SpacesLookbackDays = Math.Max(1, SpacesLookbackDays);
        state.ContributorsLookbackDays = Math.Max(1, ContributorsLookbackDays);
        SaveState(state);
        return RedirectToPage("TestData");
    }
}
