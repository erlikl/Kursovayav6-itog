using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BackEnd.Models.Employees
{
    public class Education
    {
        public int Id { get; set; }
        public string Institution { get; set; }
        public string Degree { get; set; }
        public string Specialization { get; set; }
        public int YearOfGraduation { get; set; }
        public int EmployeeId { get; set; }
        //public virtual Employee Employee { get; set; }
    }
}