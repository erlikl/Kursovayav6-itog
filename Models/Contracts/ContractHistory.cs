using System;

namespace BackEnd.Models.Contracts
{
    public class ContractHistory
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public Contract Contract { get; set; }
        public string Action { get; set; }
        public string PerformedBy { get; set; }
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
        public string Details { get; set; }
    }
}
