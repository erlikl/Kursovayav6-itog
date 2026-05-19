using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BackEnd.DbContexts;
using BackEnd.Models.Contracts;
using BackEnd.Models.DTO;
using System.Threading.Tasks;
using System.Linq;

namespace BackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize]
    public class ContractPartiesController : ControllerBase
    {
        private readonly ApplicationContext _context;
        public ContractPartiesController(ApplicationContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet]
        public IActionResult GetAll()
        {
            var list = _context.ContractParties.OrderBy(p => p.Name).Select(p => new ContractPartyResponseDto
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type,
                Representative = p.Representative,
                Email = p.Email,
                Phone = p.Phone,
                Address = p.Address,
                TaxId = p.TaxId,
                Notes = p.Notes
            }).ToList();
            return Ok(list);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var p = await _context.ContractParties.FindAsync(id);
            if (p == null) return NotFound();
            return Ok(new ContractPartyResponseDto
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type,
                Representative = p.Representative,
                Email = p.Email,
                Phone = p.Phone,
                Address = p.Address,
                TaxId = p.TaxId,
                Notes = p.Notes
            });
        }

        [Authorize(Roles = "admin,manager")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ContractPartyRequestDto request)
        {
            var p = new ContractParty
            {
                Name = request.Name,
                Type = request.Type,
                Representative = request.Representative,
                Email = request.Email,
                Phone = request.Phone,
                Address = request.Address,
                TaxId = request.TaxId,
                Notes = request.Notes
            };
            _context.ContractParties.Add(p);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = p.Id }, p);
        }

        [Authorize(Roles = "admin,manager")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ContractPartyRequestDto request)
        {
            var p = await _context.ContractParties.FindAsync(id);
            if (p == null) return NotFound();
            p.Name = request.Name;
            p.Type = request.Type;
            p.Representative = request.Representative;
            p.Email = request.Email;
            p.Phone = request.Phone;
            p.Address = request.Address;
            p.TaxId = request.TaxId;
            p.Notes = request.Notes;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _context.ContractParties.FindAsync(id);
            if (p == null) return NotFound();
            _context.ContractParties.Remove(p);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
