using System.Net.Http.Json;
using Snakk.Application.DTOs.Management;
using Snakk.Sdk;

using UpdateSpaceSettingsRequest = Snakk.Application.DTOs.Management.UpdateSpaceSettingsRequest;

namespace Snakk.AdminWeb.Services;

public class AdminApiClientService(SnakkApiClient client, HttpClient httpClient)
{
    public SnakkApiClient Client => client;

    public async Task<SpaceSettingsDto?> GetSpaceSettingsAsync(string spacePublicId)
    {
        var response = await httpClient.GetAsync($"/spaces/{spacePublicId}/manage/settings");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SpaceSettingsDto>();
    }

    public async Task<bool> UpdateSpaceSettingsAsync(string spacePublicId, UpdateSpaceSettingsRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"/spaces/{spacePublicId}/manage/settings", request);
        return response.IsSuccessStatusCode;
    }
}
