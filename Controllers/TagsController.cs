using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BackEnd.DbContexts;
using BackEnd.Models.Contracts;
using BackEnd.Models.DTO;
using System.Linq;
using System.Threading.Tasks;

namespace BackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize]
    public class TagsController : ControllerBase
    {
        private readonly ApplicationContext _context;
        public TagsController(ApplicationContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet]
        public IActionResult GetAll()
        {
            var list = _context.Tags.OrderBy(t => t.Name).Select(t => new TagResponseDto { Id = t.Id, Name = t.Name }).ToList();
            return Ok(list);
        }

        [Authorize(Roles = "admin,manager")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TagRequestDto request)
        {
            var tag = new Tag { Name = request.Name };
            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAll), new { id = tag.Id }, new TagResponseDto { Id = tag.Id, Name = tag.Name });
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var tag = await _context.Tags.FindAsync(id);
            if (tag == null) return NotFound();
            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
