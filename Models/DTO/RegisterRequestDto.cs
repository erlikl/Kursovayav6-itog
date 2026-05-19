using System.ComponentModel.DataAnnotations;

namespace BackEnd.Models.DTO
{
    public class RegisterRequestDto
    {
        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Email must be a valid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "FullName is required")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "FullName must be between 1 and 200 characters")]
        public string FullName { get; set; } = string.Empty;
    }
}