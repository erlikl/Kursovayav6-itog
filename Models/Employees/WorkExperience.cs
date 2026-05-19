namespace BackEnd.Models.Employees
{
    public class WorkExperience
    {
        public int Id { get; set; }
        public string CompanyName { get; set; }
        public string Position { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Responsibilities { get; set; }
        public int EmployeeId { get; set; }
        //public virtual Employee Employee { get; set; }
    }
}
