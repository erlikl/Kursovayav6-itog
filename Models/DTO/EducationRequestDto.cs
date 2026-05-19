using System.ComponentModel.DataAnnotations;

namespace BackEnd.Models.DTO
{
    public class EducationRequestDto
    {
        [Required(ErrorMessage = "Institution is required")]
        public string Institution { get; set; } = string.Empty;

        [Required(ErrorMessage = "Degree is required")]
        public string Degree { get; set; } = string.Empty;

        [Required(ErrorMessage = "Specialization is required")]
        public string Specialization { get; set; } = string.Empty;

        [Required(ErrorMessage = "YearOfGraduation is required")]
        [Range(1950, 2100, ErrorMessage = "YearOfGraduation must be between 1950 and 2100")]
        public int YearOfGraduation { get; set; }
    }
}