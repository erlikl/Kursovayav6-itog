using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BackEnd.DbContexts;
using BackEnd.Models.Departments;
using Microsoft.EntityFrameworkCore;

namespace BackEnd.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    [Authorize]  
    public class DepartmentsController : ControllerBase
    {
        private readonly ApplicationContext _context;

        public DepartmentsController(ApplicationContext context)
        {
            _context = context;
        }

        // GET: api/departments - get all departments
        [HttpGet]
        [Authorize(Roles = "admin,manager")]
        public async Task<IActionResult> GetDepartments()
        {
            var departments = await _context.Departments
                .Include(d => d.Employees) 
                .ToListAsync();
            return Ok(departments);
        }

        // GET: api/departments/{id} - get department by ID
        [HttpGet("{id}")]
        [Authorize(Roles = "admin,manager")]
        public async Task<IActionResult> GetDepartment(int id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null)
                return NotFound(new { error = "Department not found" });

            return Ok(department);
        }

        // POST: api/departments - create department
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AddDepartment([FromBody] Department department)
        {
            if (string.IsNullOrEmpty(department.Name))
                return BadRequest(new { error = "Department name is required" });

            _context.Departments.Add(department);
            await _context.SaveChangesAsync();

            return Ok(department);
        }

        // PUT: api/departments/{id} - update department
        [HttpPut("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateDepartment(int id, [FromBody] Department updatedDepartment)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null)
                return NotFound(new { error = "Department not found" });

            department.Name = updatedDepartment.Name;
            department.Description = updatedDepartment.Description;

            await _context.SaveChangesAsync();
            return Ok(department);
        }

        // DELETE: api/departments/{id} - delete department
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null)
                return NotFound(new { error = "Department not found" });

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}