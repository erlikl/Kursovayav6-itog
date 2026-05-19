using System;
using System.Collections.Generic;
using BackEnd.Models.Employees;

namespace BackEnd.Models.Contracts
{
    public class Contract
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string ContractNumber { get; set; }

        // Parties (simple approach: normalized party entities)
        public int? PartyAId { get; set; }
        public ContractParty PartyA { get; set; }

        public int? PartyBId { get; set; }
        public ContractParty PartyB { get; set; }

        public int? DepartmentId { get; set; }
        public int? ResponsibleEmployeeId { get; set; }
        public Employee ResponsibleEmployee { get; set; }

        public string ContractType { get; set; }
        public string Status { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public decimal? Amount { get; set; }
        public string Currency { get; set; }

        public string Description { get; set; }

        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ContractHistory> Histories { get; set; }
        public ICollection<Tag> Tags { get; set; }
    }
}
