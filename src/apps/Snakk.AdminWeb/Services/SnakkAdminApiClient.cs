using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Snakk.AdminWeb.Services;

public class SnakkAdminApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JsonSerializerOptions _jsonOptions;

    public SnakkAdminApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private void SetAuthToken()
    {
        var token = _httpContextAccessor.HttpContext?.Request.Cookies["admin_token"];
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        SetAuthToken();
        var response = await _httpClient.GetAsync(endpoint);

        if (!response.IsSuccessStatusCode)
            return default;

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, _jsonOptions);
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string endpoint, T data)
    {
        SetAuthToken();
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _httpClient.PostAsync(endpoint, content);
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string endpoint, T data)
    {
        SetAuthToken();
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _httpClient.PutAsync(endpoint, content);
    }

    public async Task<HttpResponseMessage> DeleteAsync(string endpoint)
    {
        SetAuthToken();
        return await _httpClient.DeleteAsync(endpoint);
    }

    // Admin Auth
    public async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        var response = await PostAsync("/api/admin/auth/login", new { email, password });
        return response;
    }

    // Communities
    public async Task<List<CommunityDto>?> GetCommunitiesAsync()
    {
        return await GetAsync<List<CommunityDto>>("/api/communities");
    }

    public async Task<CommunityDto?> GetCommunityAsync(string slug)
    {
        return await GetAsync<CommunityDto>($"/api/communities/{slug}");
    }

    public async Task<HttpResponseMessage> CreateCommunityAsync(CreateCommunityRequest request)
    {
        return await PostAsync("/api/communities", request);
    }

    public async Task<HttpResponseMessage> UpdateCommunityAsync(string slug, UpdateCommunityRequest request)
    {
        return await PutAsync($"/api/communities/{slug}", request);
    }

    public async Task<HttpResponseMessage> DeleteCommunityAsync(string slug)
    {
        return await DeleteAsync($"/api/communities/{slug}");
    }

    // Hubs
    public async Task<List<HubDto>?> GetHubsAsync(string communitySlug)
    {
        return await GetAsync<List<HubDto>>($"/api/communities/{communitySlug}/hubs");
    }

    public async Task<HubDto?> GetHubAsync(string communitySlug, string hubSlug)
    {
        return await GetAsync<HubDto>($"/api/communities/{communitySlug}/hubs/{hubSlug}");
    }

    public async Task<HttpResponseMessage> CreateHubAsync(string communitySlug, CreateHubRequest request)
    {
        return await PostAsync($"/api/communities/{communitySlug}/hubs", request);
    }

    public async Task<HttpResponseMessage> UpdateHubAsync(string communitySlug, string hubSlug, UpdateHubRequest request)
    {
        return await PutAsync($"/api/communities/{communitySlug}/hubs/{hubSlug}", request);
    }

    public async Task<HttpResponseMessage> DeleteHubAsync(string communitySlug, string hubSlug)
    {
        return await DeleteAsync($"/api/communities/{communitySlug}/hubs/{hubSlug}");
    }

    // Spaces
    public async Task<List<SpaceDto>?> GetSpacesAsync(string communitySlug, string hubSlug)
    {
        return await GetAsync<List<SpaceDto>>($"/api/communities/{communitySlug}/hubs/{hubSlug}/spaces");
    }

    public async Task<SpaceDto?> GetSpaceAsync(string communitySlug, string spaceSlug)
    {
        return await GetAsync<SpaceDto>($"/api/communities/{communitySlug}/spaces/{spaceSlug}");
    }

    public async Task<HttpResponseMessage> CreateSpaceAsync(string communitySlug, string hubSlug, CreateSpaceRequest request)
    {
        return await PostAsync($"/api/communities/{communitySlug}/hubs/{hubSlug}/spaces", request);
    }

    public async Task<HttpResponseMessage> UpdateSpaceAsync(string communitySlug, string spaceSlug, UpdateSpaceRequest request)
    {
        return await PutAsync($"/api/communities/{communitySlug}/spaces/{spaceSlug}", request);
    }

    public async Task<HttpResponseMessage> DeleteSpaceAsync(string communitySlug, string spaceSlug)
    {
        return await DeleteAsync($"/api/communities/{communitySlug}/spaces/{spaceSlug}");
    }

    // Users
    public async Task<UserListDto?> GetUsersAsync(int page = 1, int pageSize = 50)
    {
        return await GetAsync<UserListDto>($"/api/admin/users?page={page}&pageSize={pageSize}");
    }

    public async Task<UserDto?> GetUserAsync(string userId)
    {
        return await GetAsync<UserDto>($"/api/admin/users/{userId}");
    }

    // Moderation
    public async Task<List<ReportDto>?> GetReportsAsync()
    {
        return await GetAsync<List<ReportDto>>("/api/admin/moderation/reports");
    }
}

// DTOs (simplified - should match backend)
public record CommunityDto(string Slug, string Name, string? Description, DateTime CreatedAt);
public record HubDto(int Id, string Slug, string Name, string? Description);
public record SpaceDto(int Id, string Slug, string Name, string? Description);
public record UserDto(string PublicId, string DisplayName, string? Email, DateTime CreatedAt);
public record UserListDto(List<UserDto> Users, int Total, int Page, int PageSize);
public record ReportDto(int Id, string Type, string Reason, string Status, DateTime CreatedAt);

// Request models
public record CreateCommunityRequest(string Name, string Slug, string? Description);
public record UpdateCommunityRequest(string Name, string? Description);
public record CreateHubRequest(string Name, string Slug, string? Description);
public record UpdateHubRequest(string Name, string? Description);
public record CreateSpaceRequest(string Name, string Slug, string? Description);
public record UpdateSpaceRequest(string Name, string? Description);
