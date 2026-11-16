using Isopoh.Cryptography.Argon2;

namespace GalgameOnline.SdkServer.Services;

/// <summary>Argon2id password hashing. Only the encoded hash is ever stored.</summary>
public static class PasswordHasher
{
    /// <summary>Hash a plaintext password. Returns the encoded Argon2id string.</summary>
    public static string Hash(string password) => Argon2.Hash(password);

    /// <summary>Verify a plaintext password against a stored encoded hash.</summary>
    public static bool Verify(string encodedHash, string password)
    {
        try
        {
            return Argon2.Verify(encodedHash, password);
        }
        catch
        {
            // Malformed stored hash: treat as a failed verification, never crash.
            return false;
        }
    }
}
