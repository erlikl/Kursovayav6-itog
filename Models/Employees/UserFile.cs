using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BackEnd.Models.Employees
{
    public class UserFile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string SystemName { get; set; } = string.Empty; // имя файла на сервере
        public string DisplayName { get; set; } = string.Empty; // оригинальное имя файла

        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        // Optional link to Contract
        public int? ContractId { get; set; }
        // navigation property not required for basic usage

        public DateTime UploadDate { get; set; }
    }
}
