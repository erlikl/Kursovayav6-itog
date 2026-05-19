using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using BackEnd;
using BackEnd.DbContexts;
using BackEnd.Models.DTO;
using BackEnd.Models.Users;
using BackEnd.Models.Employees;
using BackEnd.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BackEnd.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationContext _context;

        public AuthController(ApplicationContext context)
        {
            _context = context;
        }

        // Метод для регистрации - принимает username и password
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            // Проверка на пустые поля
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { error = "Username and password are required" });
            }

            // Проверка на существующий логин
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Login == request.Username);
            if (existingUser != null)
            {
                return BadRequest(new { error = "User with this username already exists" });
            }

            //Проверка на существующий email
            var existingEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingEmail != null)
            {
                return BadRequest(new { error = "User with this email already exists" });
            }

            // Создаём пользователя
            var newUser = new User
            {
                Login = request.Username,
                Password = AuthUtils.HashPassword(request.Password),
                Role = "user",
                Email = request.Email,
                FullName = request.FullName
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Автоматически создать запись Employee, связав по Username
            try
            {
                var employee = new Employee
                {
                    FirstName = request.FullName ?? string.Empty,
                    LastName = string.Empty,
                    Patronymic = string.Empty,
                    Email = request.Email ?? string.Empty,
                    PhoneNumber = string.Empty,
                    BirthDate = DateTime.MinValue,
                    Username = request.Username,
                    DepartmentId = null
                };
                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();

                return Ok(new { message = "User and employee created successfully", username = request.Username, employeeId = employee.Id });
            }
            catch (Exception ex)
            {
                // Если создание Employee не удалось, вернуть успешную регистрацию пользователя, но сообщить об ошибке создания сотрудника
                return Ok(new { message = "User registered but failed to create employee record", username = request.Username, error = ex.Message });
            }
        }

        // Метод для получения данных пользователя
        private ClaimsIdentity? GetIdentity(string username, string password)
        {
            User? user = _context.Users.FirstOrDefault(u => u.Login == username);

            if (user != null)
            {
                if (AuthUtils.VerifyPassword(password, user.Password))
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimsIdentity.DefaultNameClaimType, user.Login),
                        new Claim(ClaimsIdentity.DefaultRoleClaimType, user.Role)
                    };
                    return new ClaimsIdentity(claims, "Token",
                        ClaimsIdentity.DefaultNameClaimType,
                        ClaimsIdentity.DefaultRoleClaimType);
                }
            }
            return null;
        }

        // Метод для входа - принимает username и password
        [HttpPost("login")]
        public IActionResult Login([FromForm] string username, [FromForm] string password)
        {
            try
            {
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    return BadRequest(new { error = "Username and password are required" });
                }

                var identity = GetIdentity(username, password);
                if (identity == null)
                {
                    return BadRequest(new { error = "Invalid username or password" });
                }

                var user = _context.Users.FirstOrDefault(u => u.Login == username);
                if (user == null)
                {
                    return BadRequest(new { error = "User not found" });
                }

                var employee = _context.Employees
                    .Include(x => x.Educations)
                    .Include(x => x.WorkExperiences)
                    .FirstOrDefault(x => x.Username == user.Login);

                var now = DateTime.UtcNow;
                var jwt = new JwtSecurityToken(
                    issuer: AuthOptions.ISSUER,
                    audience: AuthOptions.AUDIENCE,
                    notBefore: now,
                    claims: identity.Claims,
                    expires: now.Add(TimeSpan.FromMinutes(AuthOptions.LIFETIME)),
                    signingCredentials: new SigningCredentials(
                        AuthOptions.GetSymmetricSecurityKey(),
                        SecurityAlgorithms.HmacSha256));

                var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

                var response = new
                {
                    access_token = encodedJwt,
                    username = username,
                    role = user.Role,
                    email = user.Email,
                    fullName = user.FullName
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Login error: " + ex.Message });
            }
        }
    }
}