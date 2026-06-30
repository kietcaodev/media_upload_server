using System.Security.Cryptography;
using System.Text;
using MediaUpload.Domain.Interfaces;

namespace MediaUpload.Infrastructure.Services;

public class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(string base64Key)
    {
        var keyBytes = Convert.FromBase64String(base64Key);
        if (keyBytes.Length != 32)
            throw new ArgumentException("AES key must be 32 bytes (256-bit base64 encoded).");
        _key = keyBytes;
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        var data = Convert.FromBase64String(cipherText);
        using var aes = Aes.Create();
        aes.Key = _key;
        var iv = new byte[aes.BlockSize / 8];
        var cipher = new byte[data.Length - iv.Length];
        Buffer.BlockCopy(data, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(data, iv.Length, cipher, 0, cipher.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
