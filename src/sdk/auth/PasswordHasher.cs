using System.Security.Cryptography;

namespace dnproto.sdk.auth
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16; // 128 bits
        private const int HashSize = 32; // 256 bits
        private const int Iterations = 100000; // OWASP recommendation (minimum)

        /// <summary>
        /// Hashes a password using PBKDF2 with SHA256.
        /// </summary>
        /// <param name="password">The password to hash.</param>
        /// <returns>A base64-encoded string containing the salt and hash.</returns>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty.", nameof(password));
            }

            // Generate a random salt
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            // Hash the password with PBKDF2
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            // Combine salt and hash
            byte[] hashBytes = new byte[SaltSize + HashSize];
            Array.Copy(salt, 0, hashBytes, 0, SaltSize);
            Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

            // Convert to base64 for storage
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Verifies a password against a stored hash.
        /// </summary>
        /// <param name="hashedPassword">The stored hash (base64-encoded).</param>
        /// <param name="password">The password to verify.</param>
        /// <returns>True if the password matches the hash, otherwise false.</returns>
        public static bool VerifyPassword(string hashedPassword, string password)
        {
            if (string.IsNullOrEmpty(hashedPassword))
            {
                throw new ArgumentException("Hashed password cannot be null or empty.", nameof(hashedPassword));
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty.", nameof(password));
            }

            try
            {
                // Decode the base64 hash
                byte[] hashBytes = Convert.FromBase64String(hashedPassword);

                // Ensure the hash is the correct length
                if (hashBytes.Length != SaltSize + HashSize)
                {
                    return false;
                }

                // Extract the salt
                byte[] salt = new byte[SaltSize];
                Array.Copy(hashBytes, 0, salt, 0, SaltSize);

                // Extract the stored hash
                byte[] storedHash = new byte[HashSize];
                Array.Copy(hashBytes, SaltSize, storedHash, 0, HashSize);

                // Hash the input password with the same salt
                byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    Iterations,
                    HashAlgorithmName.SHA256,
                    HashSize);

                // Use constant-time comparison to prevent timing attacks
                return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
            }
            catch
            {
                // If there's any error decoding or processing, the password doesn't match
                return false;
            }
        }
    }
}