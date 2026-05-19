using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace BackEnd
{
    public class AuthOptions
    {
        public const string ISSUER = "BackEnd_App_Server";        // издатель токена
        public const string AUDIENCE = "BackEnd_App_Client";      // потребитель токена
        private const string KEY = "CA555C6D-21F5-4990-A3C4-BA3B9593D37C"; // ключ шифрования
        public const int LIFETIME = 1440;                            // время жизни токена (в минутах)

        
        public static SymmetricSecurityKey GetSymmetricSecurityKey()
        {
            return new SymmetricSecurityKey(Encoding.ASCII.GetBytes(KEY));
        }
    }
}