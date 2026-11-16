// Auth helper shared by all protected endpoints.

using GalgameOnline.GameServer.Services;

namespace GalgameOnline.GameServer;

public static class AuthHelper
{
    /// <summary>
    /// Resolves the AccountId from the Authorization: Bearer header via the
    /// SdkServer. Returns (AccountId, null) on success, (null, error result)
    /// otherwise: 401 for missing/invalid tokens, 503 when the SdkServer
    /// cannot be reached.
    /// </summary>
    public static async Task<(Guid? AccountId, IResult? Error)> AuthenticateAsync(
        HttpRequest req, SdkSessionClient sdk)
    {
        var auth = req.Headers.Authorization.ToString();
        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return (null, Results.Unauthorized());

        var token = auth["Bearer ".Length..].Trim();
        if (token.Length == 0)
            return (null, Results.Unauthorized());

        try
        {
            var id = await sdk.GetAccountIdAsync(token);
            return id is null ? (null, Results.Unauthorized()) : (id, null);
        }
        catch (HttpRequestException)
        {
            return (null, Results.StatusCode(503));
        }
        catch (TaskCanceledException)
        {
            return (null, Results.StatusCode(503));
        }
    }
}
