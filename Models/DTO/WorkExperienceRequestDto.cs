using System.ComponentModel.DataAnnotations;

namespace BackEnd.Models.DTO
{
    public class WorkExperienceRequestDto
    {
        [Required(ErrorMessage = "CompanyName is required")]
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Position is required")]
        public string Position { get; set; } = string.Empty;

        [Required(ErrorMessage = "StartDate is required")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "EndDate is required")]
        public DateTime EndDate { get; set; }

        public string Responsibilities { get; set; } = string.Empty;
    }
}