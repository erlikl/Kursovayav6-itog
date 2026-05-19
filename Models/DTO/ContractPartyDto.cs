namespace BackEnd.Models.DTO
{
    public class ContractPartyRequestDto
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Representative { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string TaxId { get; set; }
        public string Notes { get; set; }
    }

    public class ContractPartyResponseDto
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
