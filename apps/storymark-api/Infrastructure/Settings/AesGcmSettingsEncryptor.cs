using System.Security.Cryptography;
using System.Text;

namespace Storymark.Api.Infrastructure.Settings;

/// <summary>AES-256-GCM. The stored blob is base64(nonce ‖ tag ‖ ciphertext).
/// The key comes from <c>Settings:EncryptionKey</c> (base64 32 bytes).</summary>
internal sealed class AesGcmSettingsEncryptor : ISettingsEncryptor
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly byte[]? _key;

    public AesGcmSettingsEncryptor(IConfiguration configuration)
    {
        var encoded = configuration["Settings:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(encoded))
        {
            _key = null;
            return;
        }

        var key = Convert.FromBase64String(encoded);
        if (key.Length != KeySize)
        {
            throw new InvalidOperationException(
                "Settings:EncryptionKey must be a base64-encoded 32-byte key (AES-256-GCM).");
        }

        _key = key;
    }

    public string Encrypt(string plaintext)
    {
        var key = RequireKey();
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var combined = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, combined, NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string ciphertext)
    {
        var key = RequireKey();
        var combined = Convert.FromBase64String(ciphertext);
        if (combined.Length < NonceSize + TagSize)
        {
            throw new InvalidOperationException("Encrypted setting value is malformed.");
        }

        var nonce = combined.AsSpan(0, NonceSize);
        var tag = combined.AsSpan(NonceSize, TagSize);
        var cipher = combined.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private byte[] RequireKey() => _key
        ?? throw new InvalidOperationException(
            "Settings:EncryptionKey is not configured; it is required to read or write secret settings.");
}
