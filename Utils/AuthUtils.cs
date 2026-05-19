using System.Security.Cryptography;
using System.Text;

namespace BackEnd.Utils
{
    public static class AuthUtils
    {
        // Метод для хеширования пароля (шифрование)
        public static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(password);
                byte[] hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        // Метод для проверки пароля
        public static bool VerifyPassword(string inputPassword, string storedPasswordHash)
        {
            string inputHash = HashPassword(inputPassword);
            return inputHash == storedPasswordHash;
        }
    }
}