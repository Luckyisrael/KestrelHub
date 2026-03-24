using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace KestrelHub.Dashboard.Services;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigation;
    private string? _accessToken;

    public JwtAuthStateProvider(HttpClient httpClient, NavigationManager navigation)
    {
        _httpClient = httpClient;
        _navigation = navigation;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
        {
            return CreateAnonymousState();
        }

        try
        {
            var claims = ParseClaims(_accessToken);
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "jwt");
            var user = new System.Security.Claims.ClaimsPrincipal(identity);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            return new AuthenticationState(user);
        }
        catch
        {
            _accessToken = null;
            return CreateAnonymousState();
        }
    }

    public async Task<bool> TrySilentRefreshAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("/api/auth/refresh", null);
            if (!response.IsSuccessStatusCode)
                return false;

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            _accessToken = body.GetProperty("accessToken").GetString();
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
            if (!response.IsSuccessStatusCode)
                return false;

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            _accessToken = body.GetProperty("accessToken").GetString();
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _httpClient.PostAsync("/api/auth/logout", null);
        }
        catch { }

        _accessToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);
    public string? AccessToken => _accessToken;

    private static AuthenticationState CreateAnonymousState()
    {
        return new AuthenticationState(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()));
    }

    private static IEnumerable<System.Security.Claims.Claim> ParseClaims(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            yield break;

        var payload = parts[1];
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var doc = JsonDocument.Parse(json);

        foreach (var claim in doc.RootElement.EnumerateObject())
        {
            if (claim.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in claim.Value.EnumerateArray())
                    yield return new System.Security.Claims.Claim(claim.Name, item.GetString()!);
            }
            else
            {
                yield return new System.Security.Claims.Claim(claim.Name, claim.Value.ToString());
            }
        }
    }
}
