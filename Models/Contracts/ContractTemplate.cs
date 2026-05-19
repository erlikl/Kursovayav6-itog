using System;

namespace BackEnd.Models.Contracts
{
    public class ContractTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Content { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
