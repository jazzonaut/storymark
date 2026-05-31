namespace Storymark.Api.Infrastructure.Settings;

internal interface ISettingsEncryptor
{
    string Encrypt(string plaintext);

    string Decrypt(string ciphertext);
}
