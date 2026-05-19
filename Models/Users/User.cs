using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BackEnd.Models.Users
{
    public class User
    {
        [Key]  // Указываем, что это первичный ключ
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]  // Автоматическая генерация значения БД
        public int Id { get; set; }  // Идентификатор пользователя

        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        // Дополнительное поле для отображения ФИО пользователя
        public string FullName { get; set; } = string.Empty;
    }
}