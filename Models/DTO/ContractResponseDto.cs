using System;

namespace BackEnd.Models.DTO
{
    public class ContractResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string ContractNumber { get; set; }
        public int? PartyAId { get; set; }
        public int? PartyBId { get; set; }
        public int? DepartmentId { get; set; }
        public int? ResponsibleEmployeeId { get; set; }
        public string ContractType { get; set; }
        public string Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? Amount { get; set; }
        public string Currency { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedByFullName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
