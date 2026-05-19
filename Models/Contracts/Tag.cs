namespace BackEnd.Models.Contracts
{
    public class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ICollection<Contract> Contracts { get; set; }
    }
}
