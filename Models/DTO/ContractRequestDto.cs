using System;
using System.ComponentModel.DataAnnotations;

namespace BackEnd.Models.DTO
{
    public class ContractRequestDto
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(500, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 500 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "ContractNumber is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "ContractNumber must be between 1 and 100 characters")]
        public string ContractNumber { get; set; }

        public int? PartyAId { get; set; }
        public int? PartyBId { get; set; }
        public int? DepartmentId { get; set; }
        public int? ResponsibleEmployeeId { get; set; }

        [Required(ErrorMessage = "ContractType is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "ContractType must be between 1 and 100 characters")]
        public string ContractType { get; set; }

        public string Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? Amount { get; set; }
        public string Currency { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = "Description must be between 1 and 2000 characters")]
        public string Description { get; set; }
    }
}
