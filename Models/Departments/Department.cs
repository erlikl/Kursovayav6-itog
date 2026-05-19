using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BackEnd.Models.Employees;

namespace BackEnd.Models.Departments
{
    public class Department
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }  // Может быть пустым при помощи ?

        // Связь с сотрудниками (один ко многим)
        public virtual ICollection<Employee>? Employees { get; set; }
    }
}