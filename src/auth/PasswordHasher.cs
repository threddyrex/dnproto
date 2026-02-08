using System.Security.Cryptography;

namespace dnproto.auth
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16; // 128 bits
        private const int HashSize = 32; // 256 bits
        private const int Iterations = 100000; // OWASP recommendation (minimum)

        /// <summary>
        /// Creates a new cryptographically secure random password suitable for admin accounts.
        /// </summary>
        /// <returns>A 32-character password with uppercase, lowercase, numbers, and special characters.</returns>
        public static string CreateNewAdminPassword()
        {
            const int passwordLength = 64;
            const string uppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercaseChars = "abcdefghijklmnopqrstuvwxyz";
            const string numberChars = "0123456789";
            const string specialChars = "!@#$%^&*()-_=+[]{}|;:,.<>?";
            const string allChars = uppercaseChars + lowercaseChars + numberChars + specialChars;

            // Ensure at least one character from each category
            char[] password = new char[passwordLength];
            password[0] = uppercaseChars[RandomNumberGenerator.GetInt32(uppercaseChars.Length)];
            password[1] = lowercaseChars[RandomNumberGenerator.GetInt32(lowercaseChars.Length)];
            password[2] = numberChars[RandomNumberGenerator.GetInt32(numberChars.Length)];
            password[3] = specialChars[RandomNumberGenerator.GetInt32(specialChars.Length)];

            // Fill the rest with random characters from all categories
            for (int i = 4; i < passwordLength; i++)
            {
                password[i] = allChars[RandomNumberGenerator.GetInt32(allChars.Length)];
            }

            // Shuffle the password to randomize position of guaranteed characters
            for (int i = passwordLength - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }

            return new string(password);
        }

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
        public static bool VerifyPassword(string? hashedPassword, string? password)
        {
            if (string.IsNullOrEmpty(hashedPassword))
            {
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                return false;
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