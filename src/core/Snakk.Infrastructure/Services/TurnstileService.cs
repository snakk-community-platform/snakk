namespace Snakk.Infrastructure.Services;

using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;

public class TurnstileService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<TurnstileService> logger) : ITurnstileService
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public async Task<bool> VerifyAsync(string token, string? remoteIp = null)
    {
        var secretKey = configuration["Turnstile:SecretKey"];

        // If no secret key is configured, skip verification (development mode)
        if (string.IsNullOrEmpty(secretKey))
        {
            logger.LogWarning("Turnstile secret key not configured — skipping verification");
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var client = httpClientFactory.CreateClient();

            var payload = new Dictionary<string, string>
            {
                ["secret"] = secretKey,
                ["response"] = token
            };

            if (!string.IsNullOrEmpty(remoteIp))
                payload["remoteip"] = remoteIp;

            var response = await client.PostAsync(VerifyUrl, new FormUrlEncodedContent(payload));
            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>();

            if (result?.Success != true)
                logger.LogWarning("Turnstile verification failed: {Errors}", string.Join(", ", result?.ErrorCodes ?? []));

            return result?.Success == true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Turnstile verification error");
            return false;
        }
    }

    private record TurnstileResponse
    {
        public bool Success { get; init; }
        public string[]? ErrorCodes { get; init; }
    }
}
