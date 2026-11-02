using System.Security.Cryptography;

namespace Backyard_Mech.Services
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100_000;

        public static string Hash(string password)
        {
            return new Microsoft.AspNetCore.Identity.PasswordHasher<object>().HashPassword(new object(), password);
        }

        public static bool Verify(string password, string passwordHash)
        {
            try
            {
            if (!passwordHash.Contains('.')) return new Microsoft.AspNetCore.Identity.PasswordHasher<object>()
                .VerifyHashedPassword(new object(), passwordHash, password) != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
            var parts = passwordHash.Split('.', 3);
            if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations) || iterations is < 10000 or > 2000000)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[1]);
            var expectedKey = Convert.FromBase64String(parts[2]);
            var actualKey = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedKey.Length);

            return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
            }
            catch (Exception e) when (e is FormatException or ArgumentException or CryptographicException) { return false; }
        }
    }
}
