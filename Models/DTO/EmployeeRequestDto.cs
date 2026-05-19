using System.ComponentModel.DataAnnotations;

namespace BackEnd.Models.DTO
{
    public class EmployeeRequestDto
    {
        [Required(ErrorMessage = "FirstName is required")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "LastName is required")]
        public string LastName { get; set; } = string.Empty;  

        public string Patronymic { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email must be a valid email address")]
        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "BirthDate is required")]
        public DateTime BirthDate { get; set; }

        [Required(ErrorMessage = "DepartmentId is required")]
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = string.Empty;
    }
}