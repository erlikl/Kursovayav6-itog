using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using BackEnd.DbContexts;
using BackEnd.Models.Employees;
using BackEnd.Models.Departments;
using BackEnd.Models.DTO;

namespace BackEnd.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    [Authorize]
    public class EmployeesController : ControllerBase
    {
        private readonly ApplicationContext _context;

        public EmployeesController(ApplicationContext context)
        {
            _context = context;
        }

        // GET: api/employees - get all employees with their education and experience
        [HttpGet]
        [Authorize(Roles = "admin,manager")]
        public async Task<IActionResult> GetEmployees()
        {
            var employees = await _context.Employees
                .Include(e => e.Educations)
                .Include(e => e.WorkExperiences)
                .ToListAsync();

            return Ok(employees);
        }

        // GET: api/employees/{id} - get employee by ID
        [HttpGet("{id}")]
        [Authorize(Roles = "admin,manager")]
        public async Task<IActionResult> GetEmployee(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.Educations)
                .Include(e => e.WorkExperiences)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound(new { error = "Employee not found" });

            return Ok(employee);
        }

        // POST: api/employees - create employee
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreateEmployee([FromBody] EmployeeRequestDto dto)
        {
            // Verify that the department exists
            var department = await _context.Departments.FindAsync(dto.DepartmentId);
            if (department == null)
                return BadRequest(new { error = "Department not found" });

            // Verify that a user with this login exists
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == dto.Username);
            if (user == null)
                return BadRequest(new { error = "User with this login not found" });

            var employee = new Employee
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Patronymic = dto.Patronymic,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                BirthDate = dto.BirthDate,
                Username = dto.Username,
                DepartmentId = dto.DepartmentId
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return Ok(employee);
        }

        // PUT: api/employees/{id} - update employee
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateEmployee(int id, [FromBody] EmployeeRequestDto dto)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound(new { error = "Employee not found" });

            // Update fields
            employee.FirstName = dto.FirstName;
            employee.LastName = dto.LastName;
            employee.Patronymic = dto.Patronymic;
            employee.Email = dto.Email;
            employee.PhoneNumber = dto.PhoneNumber;
            employee.BirthDate = dto.BirthDate;
            employee.DepartmentId = dto.DepartmentId;

            await _context.SaveChangesAsync();
            return Ok(employee);
        }

        // DELETE: api/employees/{id} - delete employee
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound(new { error = "Employee not found" });

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/employees/{id}/workexperience - add work experience
        [HttpPost("{id}/workexperience")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AddWorkExperience(int id, [FromBody] WorkExperienceRequestDto dto)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound(new { error = "Employee not found" });

            var workExperience = new WorkExperience
            {
                CompanyName = dto.CompanyName,
                Position = dto.Position,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Responsibilities = dto.Responsibilities,
                EmployeeId = id
            };

            _context.WorkExperiences.Add(workExperience);
            await _context.SaveChangesAsync();

            return Ok(workExperience);
        }

        // DELETE: api/employees/workexperience/{weId} - delete work experience
        [HttpDelete("workexperience/{weId}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteWorkExperience(int weId)
        {
            var workExperience = await _context.WorkExperiences.FindAsync(weId);
            if (workExperience == null)
                return NotFound(new { error = "Work experience record not found" });

            _context.WorkExperiences.Remove(workExperience);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/employees/{id}/education - add education
        [HttpPost("{id}/education")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AddEducation(int id, [FromBody] EducationRequestDto dto)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
                return NotFound(new { error = "Employee not found" });

            var education = new Education
            {
                Institution = dto.Institution,
                Degree = dto.Degree,
                Specialization = dto.Specialization,
                YearOfGraduation = dto.YearOfGraduation,
                EmployeeId = id
            };

            _context.Educations.Add(education);
            await _context.SaveChangesAsync();

            return Ok(education);
        }

        // DELETE: api/employees/education/{eduId} - delete education
        [HttpDelete("education/{eduId}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteEducation(int eduId)
        {
            var education = await _context.Educations.FindAsync(eduId);
            if (education == null)
                return NotFound(new { error = "Education record not found" });

            _context.Educations.Remove(education);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}