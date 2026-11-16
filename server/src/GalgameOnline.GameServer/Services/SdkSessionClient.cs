// Validates a Bearer session token against the SdkServer.
// The GameServer never sees passwords; it trusts the SdkServer's session check.

using System.Net.Http.Headers;
using System.Net.Http.Json;
using GalgameOnline.Shared;

namespace GalgameOnline.GameServer.Services;

public sealed class SdkSessionClient(HttpClient http)
{
    /// <summary>
    /// Returns the AccountId for a valid token, or null if the token is
    /// invalid/expired. Throws HttpRequestException when the SdkServer is
    /// unreachable (callers turn that into 503).
    /// </summary>
    public async Task<Guid?> GetAccountIdAsync(string token, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/sdk/session");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            return null;

        var body = await res.Content.ReadFromJsonAsync<SessionCheckResponse>(ct);
        return body is { Ok: true, AccountId: { } id } ? id : null;
    }
}
