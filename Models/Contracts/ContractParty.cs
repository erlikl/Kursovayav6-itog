using System;

namespace BackEnd.Models.Contracts
{
    public class ContractParty
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Representative { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string TaxId { get; set; }
        public string Notes { get; set; }
    }
}
