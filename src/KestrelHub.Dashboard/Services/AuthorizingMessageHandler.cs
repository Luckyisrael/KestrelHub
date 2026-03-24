using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.Authorization;

namespace KestrelHub.Dashboard.Services;

public class AuthorizingMessageHandler : DelegatingHandler
{
    private readonly JwtAuthStateProvider _authStateProvider;

    public AuthorizingMessageHandler(JwtAuthStateProvider authStateProvider)
    {
        _authStateProvider = authStateProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Attach Bearer token if authenticated
        if (_authStateProvider.IsAuthenticated)
        {
            var token = _authStateProvider.AccessToken;
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        // On 401, try silent refresh once
        if (response.StatusCode == HttpStatusCode.Unauthorized && !request.RequestUri!.PathAndQuery.Contains("/auth/"))
        {
            var refreshed = await _authStateProvider.TrySilentRefreshAsync();
            if (refreshed)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authStateProvider.AccessToken);
                response = await base.SendAsync(request, cancellationToken);
            }
        }

        return response;
    }
}
