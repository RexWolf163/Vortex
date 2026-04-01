using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Vortex.Core.Extensions.LogicExtensions
{
    public static class Crypto
    {
        private static long _lastTime;

        private static int _counter;

        private static Random _random;
        private static Random Random => _random ??= new Random(DateTime.Now.Millisecond);


        public static string GetHashSha256(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var hashManager = new SHA256Managed();
            var hash = hashManager.ComputeHash(bytes);
            var sb = new StringBuilder();
            foreach (var x in hash)
                sb.Append($"{x:x2}");
            return sb.ToString();
        }

        public static string GetNewGuid()
        {
            var temp = DateTime.UtcNow.ToFileTimeUtc();
            if (temp == _lastTime)
                _counter++;
            else
            {
                _lastTime = temp;
                _counter = 0;
            }

            return GetHashSha256($"{temp}-{_counter}-{Random.NextDouble()}-{Random.NextDouble()}");
        }

        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int IvSize = 16;
        private const int Iterations = 100_000;

        /// <summary>
        /// Сжимает и шифрует строку. Результат — Base64.
        /// Формат: Base64( salt[16] | iv[16] | ciphertext )
        /// </summary>
        public static string SetCryptoPack(string data, string pass)
        {
            if (string.IsNullOrEmpty(data)) return data;
            if (string.IsNullOrEmpty(pass))
                throw new ArgumentNullException(nameof(pass));

            var raw = Encoding.UTF8.GetBytes(data);

            // Compress
            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionMode.Compress, true))
                    gz.Write(raw, 0, raw.Length);
                compressed = ms.ToArray();
            }

            // Key derivation
            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            byte[] key;
            using (var kdf = new Rfc2898DeriveBytes(pass, salt, Iterations, HashAlgorithmName.SHA256))
                key = kdf.GetBytes(KeySize);

            // Encrypt
            byte[] encrypted;
            byte[] iv;
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                iv = aes.IV;

                using (var encryptor = aes.CreateEncryptor())
                    encrypted = encryptor.TransformFinalBlock(compressed, 0, compressed.Length);
            }

            // Pack: salt + iv + ciphertext
            var result = new byte[SaltSize + IvSize + encrypted.Length];
            Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
            Buffer.BlockCopy(iv, 0, result, SaltSize, IvSize);
            Buffer.BlockCopy(encrypted, 0, result, SaltSize + IvSize, encrypted.Length);

            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// Расшифровывает и распаковывает данные из SetCryptoPack.
        /// </summary>
        public static string GetCryptoPack(string cryptoData, string pass)
        {
            if (string.IsNullOrEmpty(cryptoData)) return cryptoData;
            if (string.IsNullOrEmpty(pass))
                throw new ArgumentNullException(nameof(pass));

            var raw = Convert.FromBase64String(cryptoData);
            if (raw.Length < SaltSize + IvSize + 1)
                throw new ArgumentException("Invalid encrypted data.", nameof(cryptoData));

            // Unpack
            var salt = new byte[SaltSize];
            var iv = new byte[IvSize];
            var ciphertext = new byte[raw.Length - SaltSize - IvSize];
            Buffer.BlockCopy(raw, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(raw, SaltSize, iv, 0, IvSize);
            Buffer.BlockCopy(raw, SaltSize + IvSize, ciphertext, 0, ciphertext.Length);

            // Key derivation
            byte[] key;
            using (var kdf = new Rfc2898DeriveBytes(pass, salt, Iterations, HashAlgorithmName.SHA256))
                key = kdf.GetBytes(KeySize);

            // Decrypt
            byte[] compressed;
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                    compressed = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            }

            // Decompress
            using (var msInput = new MemoryStream(compressed))
            using (var gz = new GZipStream(msInput, CompressionMode.Decompress))
            using (var msOutput = new MemoryStream())
            {
                gz.CopyTo(msOutput);
                return Encoding.UTF8.GetString(msOutput.ToArray());
            }
        }
    }
}