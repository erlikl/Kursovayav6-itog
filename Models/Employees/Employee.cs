using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BackEnd.Models.Departments;

namespace BackEnd.Models.Employees
{
    public class Employee
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Patronymic { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public string Username { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public virtual Department Department { get; set; }

        public virtual ICollection<Education> Educations { get; set; }

        public virtual ICollection<WorkExperience> WorkExperiences { get; set; }

        // Файлы пользователя, прикрепленные к сотруднику
        public virtual ICollection<UserFile> UserFiles { get; set; } = new List<UserFile>();
    }
}