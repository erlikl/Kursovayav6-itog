namespace BackEnd.Models.DTO
{
    public class TagRequestDto
    {
        public string Name { get; set; }
    }

    public class TagResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
