using System.ComponentModel.DataAnnotations;

namespace BackEnd.Models.DTO
{
    public class FileRequestDto
    {
        [Required(ErrorMessage = "FileName is required")]
        public string FileName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Base64Content is required")]
        public string Base64Content { get; set; } = string.Empty;

        public int EmployeeId { get; set; }
        public int? ContractId { get; set; }
    }
}
