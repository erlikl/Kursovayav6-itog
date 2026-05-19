using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BackEnd.DbContexts;
using BackEnd.Models.DTO;
using BackEnd.Models.Contracts;
using BackEnd.Models.Employees;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using System.Text.Json;

namespace BackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ContractsController : ControllerBase
    {
        private readonly ApplicationContext _context;
        public ContractsController(ApplicationContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? departmentId = null,
            [FromQuery] string status = null,
            [FromQuery] string q = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var username = User.Identity?.Name;
                var isAdmin = User.IsInRole("admin");
                var isManager = User.IsInRole("manager");

                var query = _context.Contracts
                    .Include(c => c.ResponsibleEmployee)
                    .AsQueryable();

                // Фильтрация по правам доступа: администраторы видят все, менеджеры видят свои отделы, остальные видят только свои
                if (!isAdmin)
                {
                    var currentEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.Username == username);
                    if (currentEmployee != null)
                    {
                        if (isManager)
                        {
                            // Менеджеры видят контракты своих отделов
                            query = query.Where(c => c.DepartmentId == currentEmployee.DepartmentId);
                        }
                        else
                        {
                            // Обычные пользователи видят только контракты, за которые они отвечают
                            query = query.Where(c => c.ResponsibleEmployeeId == currentEmployee.Id);
                        }
                    }
                    else
                    {
                        return Ok(new { items = new List<ContractResponseDto>(), totalCount = 0 });
                    }
                }

                if (departmentId.HasValue)
                    query = query.Where(c => c.DepartmentId == departmentId.Value);

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(c => c.Status == status);

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var searchTerm = q.ToLower();
                    query = query.Where(c => 
                        c.Title.ToLower().Contains(searchTerm) ||
                        c.ContractNumber.ToLower().Contains(searchTerm) ||
                        c.Description.ToLower().Contains(searchTerm));
                }

                var total = await query.CountAsync();
                var items = await query.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

                // Получение полных имён создателей из таблицы Users
                var creatorUsernames = items.Select(c => c.CreatedBy).Distinct().ToList();
                var users = new Dictionary<string, string>();
                if (creatorUsernames.Count > 0)
                {
                    users = await _context.Users.Where(u => creatorUsernames.Contains(u.Login)).ToDictionaryAsync(u => u.Login, u => u.FullName);
                }

                var dtos = items.Select(c => new ContractResponseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    ContractNumber = c.ContractNumber,
                    PartyAId = c.PartyAId,
                    PartyBId = c.PartyBId,
                    DepartmentId = c.DepartmentId,
                    ResponsibleEmployeeId = c.ResponsibleEmployeeId,
                    ContractType = c.ContractType,
                    Status = c.Status,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    Amount = c.Amount,
                    Currency = c.Currency,
                    Description = c.Description,
                    CreatedAt = c.CreatedAt,
                    CreatedBy = c.CreatedBy,
                    CreatedByFullName = users.ContainsKey(c.CreatedBy) ? users[c.CreatedBy] : c.CreatedBy
                }).ToList();

                return Ok(new { items = dtos, totalCount = total });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error: " + ex.Message });
            }
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var c = await _context.Contracts
                .Include(x => x.ResponsibleEmployee)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (c == null) return NotFound();

            // Проверка прав доступа
            var username = User.Identity?.Name;
            var isAdmin = User.IsInRole("admin");
            var isManager = User.IsInRole("manager");

            if (!isAdmin && !isManager)
            {
                var currentEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.Username == username);
                if (currentEmployee == null || c.ResponsibleEmployeeId != currentEmployee.Id)
                {
                    return Forbid();
                }
            }

            // Получение полного имени создателя
            var creator = await _context.Users.FirstOrDefaultAsync(u => u.Login == c.CreatedBy);
            var createdByFullName = creator?.FullName ?? c.CreatedBy;

            var response = new ContractResponseDto
            {
                Id = c.Id,
                Title = c.Title,
                ContractNumber = c.ContractNumber,
                PartyAId = c.PartyAId,
                PartyBId = c.PartyBId,
                DepartmentId = c.DepartmentId,
                ResponsibleEmployeeId = c.ResponsibleEmployeeId,
                ContractType = c.ContractType,
                Status = c.Status,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Amount = c.Amount,
                Currency = c.Currency,
                Description = c.Description,
                CreatedAt = c.CreatedAt,
                CreatedBy = c.CreatedBy,
                CreatedByFullName = createdByFullName
            };

            // Включить названия тегов
            var tags = await _context.Tags.Where(t => t.Contracts.Any(ct => ct.Id == c.Id)).Select(t => t.Name).ToListAsync();
            // Динамическое добавление свойства Tags с помощью анонимной обертки
            return Ok(new { response, tags });
        }

        [Authorize]
        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetHistory(int id)
        {
            var contract = await _context.Contracts
                .Include(x => x.ResponsibleEmployee)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (contract == null) return NotFound();

            // Проверка прав доступа
            var username = User.Identity?.Name;
            var isAdmin = User.IsInRole("admin");
            var isManager = User.IsInRole("manager");

            if (!isAdmin && !isManager)
            {
                var currentEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.Username == username);
                if (currentEmployee == null || contract.ResponsibleEmployeeId != currentEmployee.Id)
                {
                    return Forbid();
                }
            }

            var list = await _context.ContractHistories.Where(h => h.ContractId == id).OrderByDescending(h => h.PerformedAt).ToListAsync();
            var result = list.Select(h => new { h.Id, h.ContractId, h.Action, h.PerformedBy, PerformedAt = h.PerformedAt, h.Details });
            return Ok(result);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ContractRequestDto request)
        {
            try
            {
                // Проверка состояния модели на наличие ошибок валидации
                if (!ModelState.IsValid)
                {
                    var errors = new Dictionary<string, string[]>();
                    foreach (var key in ModelState.Keys)
                    {
                        var messages = ModelState[key].Errors.Select(e => e.ErrorMessage).ToArray();
                        if (messages.Length > 0)
                        {
                            errors[key] = messages;
                        }
                    }
                    return BadRequest(new { errors });
                }

                var username = User.Identity?.Name ?? "system";

                // Проверка существования обязательных сторон контракта: миграция базы данных устанавливает PartyAId/PartyBId как не допускающие NULL
                int partyAId = request.PartyAId ?? 0;
                int partyBId = request.PartyBId ?? 0;
                if (partyAId == 0 || partyBId == 0)
                {
                    // Попробовать использовать существующую сторону, если она присутствует
                    var existing = await _context.ContractParties.FirstOrDefaultAsync();
                    if (existing != null)
                    {
                        if (partyAId == 0) partyAId = existing.Id;
                        if (partyBId == 0) partyBId = existing.Id;
                    }
                    else
                    {
                        var def = new ContractParty
                        {
                            Name = "Unknown",
                            Type = "Unknown",
                            Representative = string.Empty,
                            Email = string.Empty,
                            Phone = string.Empty,
                            Address = string.Empty,
                            TaxId = string.Empty,
                            Notes = string.Empty
                        };
                        _context.ContractParties.Add(def);
                        await _context.SaveChangesAsync();
                        if (partyAId == 0) partyAId = def.Id;
                        if (partyBId == 0) partyBId = def.Id;
                    }
                }

                // Определение идентификатора ответственного сотрудника: сначала переданный в запросе, затем текущий пользователь, затем любой существующий сотрудник
                int? respId = request.ResponsibleEmployeeId;
                if (respId == null || respId == 0)
                {
                    var empByUser = await _context.Employees.FirstOrDefaultAsync(e => e.Username == username);
                    if (empByUser != null) respId = empByUser.Id;
                    else
                    {
                        var anyEmp = await _context.Employees.FirstOrDefaultAsync();
                        if (anyEmp != null) respId = anyEmp.Id;
                        else
                            respId = null; // Разрешить NULL, если сотрудники отсутствуют
                    }
                }
                else
                {
                    // Проверка, что переданный идентификатор сотрудника существует
                    var empExists = await _context.Employees.AnyAsync(e => e.Id == respId);
                    if (!empExists)
                    {
                        return BadRequest(new { error = "Specified responsible employee does not exist" });
                    }
                }

                var contract = new Contract
                {
                    Title = request.Title ?? string.Empty,
                    ContractNumber = request.ContractNumber ?? string.Empty,
                    PartyAId = partyAId,
                    PartyBId = partyBId,
                    DepartmentId = request.DepartmentId,
                    ResponsibleEmployeeId = respId,
                    ContractType = request.ContractType ?? string.Empty,
                    Status = request.Status ?? "Draft",
                    StartDate = request.StartDate.HasValue ? DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc) : (DateTime?)null,
                    EndDate = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : (DateTime?)null,
                    Amount = request.Amount,
                    Currency = request.Currency ?? string.Empty,
                    Description = request.Description ?? string.Empty,
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = string.Empty
                };

                _context.Contracts.Add(contract);
                await _context.SaveChangesAsync();

                // Добавить запись в историю
                _context.ContractHistories.Add(new ContractHistory
                {
                    ContractId = contract.Id,
                    Action = "Created",
                    PerformedBy = username,
                    Details = "Contract created"
                });
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(Get), new { id = contract.Id }, new { contractId = contract.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to create contract: " + ex.InnerException?.Message ?? ex.Message });
            }
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ContractRequestDto request)
        {
            // Проверка состояния модели на наличие ошибок валидации
            if (!ModelState.IsValid)
            {
                var errors = new Dictionary<string, string[]>();
                foreach (var key in ModelState.Keys)
                {
                    var messages = ModelState[key].Errors.Select(e => e.ErrorMessage).ToArray();
                    if (messages.Length > 0)
                    {
                        errors[key] = messages;
                    }
                }
                return BadRequest(new { errors });
            }

            var contract = await _context.Contracts
                .Include(x => x.ResponsibleEmployee)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (contract == null) return NotFound();

            // Разрешить, если пользователь является администратором, менеджером или ответственным сотрудником
            if (!User.IsInRole("admin") && !User.IsInRole("manager"))
            {
                var username = User.Identity?.Name;
                var owner = contract.ResponsibleEmployee;
                if (owner == null || owner.Username != username)
                    return Forbid();
            }

            contract.Title = request.Title;
            contract.ContractNumber = request.ContractNumber;
            contract.PartyAId = request.PartyAId;
            contract.PartyBId = request.PartyBId;
            contract.DepartmentId = request.DepartmentId;
            contract.ResponsibleEmployeeId = request.ResponsibleEmployeeId;
            contract.ContractType = request.ContractType;
            contract.Status = request.Status;
            contract.StartDate = request.StartDate.HasValue ? DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc) : (DateTime?)null;
            contract.EndDate = request.EndDate.HasValue ? DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc) : (DateTime?)null;
            contract.Amount = request.Amount;
            contract.Currency = request.Currency;
            contract.Description = request.Description;
            contract.UpdatedAt = DateTime.UtcNow;
            contract.UpdatedBy = User.Identity?.Name;

            _context.ContractHistories.Add(new ContractHistory
            {
                ContractId = contract.Id,
                Action = "Updated",
                PerformedBy = User.Identity?.Name,
                Details = "Contract updated"
            });

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize]
        [HttpPost("{id}/attach")]
        public async Task<IActionResult> AttachFile(int id, [FromBody] AttachFileDto request)
        {
            var contract = await _context.Contracts
                .Include(x => x.ResponsibleEmployee)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (contract == null) return NotFound();

            // Только владелец, администратор или менеджер
            if (!User.IsInRole("admin") && !User.IsInRole("manager"))
            {
                var username = User.Identity?.Name;
                var owner = contract.ResponsibleEmployee;
                if (owner == null || owner.Username != username)
                    return Forbid();
            }

            var file = await _context.UserFiles.FindAsync(request.FileId);
            if (file == null) return NotFound(new { error = "File not found" });

            file.ContractId = contract.Id;
            await _context.SaveChangesAsync();

            _context.ContractHistories.Add(new ContractHistory
            {
                ContractId = contract.Id,
                Action = "FileAttached",
                PerformedBy = User.Identity?.Name,
                Details = $"File {file.Id} attached"
            });
            await _context.SaveChangesAsync();

            return Ok();
        }

        [Authorize]
        [HttpGet("{id}/files")]
        public async Task<IActionResult> GetFiles(int id)
        {
            var contract = await _context.Contracts
                .Include(x => x.ResponsibleEmployee)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (contract == null) return NotFound();

            // Проверка прав доступа
            var username = User.Identity?.Name;
            var isAdmin = User.IsInRole("admin");
            var isManager = User.IsInRole("manager");

            if (!isAdmin && !isManager)
            {
                var currentEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.Username == username);
                // Разрешить, если пользователь является ответственным за контракт ИЛИ если пользователь загрузил файлы
                var userFilesInContract = await _context.UserFiles
                    .Where(f => f.ContractId == id && f.EmployeeId == currentEmployee.Id)
                    .AnyAsync();

                if (currentEmployee == null || 
                    (contract.ResponsibleEmployeeId != currentEmployee.Id && !userFilesInContract))
                {
                    return Forbid();
                }
            }

            var files = await _context.UserFiles.Where(f => f.ContractId == id).ToListAsync();
            return Ok(files.Select(f => new { f.Id, f.DisplayName, f.SystemName, f.EmployeeId, f.UploadDate }));
        }

        [Authorize]
        [HttpPost("{id}/tags")]
        public async Task<IActionResult> AddTag(int id, [FromBody] TagRequestDto request)
        {
            var contract = await _context.Contracts
                .Include(x => x.ResponsibleEmployee)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (contract == null) return NotFound();

            // Check access rights
            var username = User.Identity?.Name;
            var isAdmin = User.IsInRole("admin");
            var isManager = User.IsInRole("manager");

            if (!isAdmin && !isManager)
            {
                var currentEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.Username == username);
                if (currentEmployee == null || contract.ResponsibleEmployeeId != currentEmployee.Id)
                {
                    return Forbid();
                }
            }

            var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == request.Name);
            if (tag == null)
            {
                tag = new Tag { Name = request.Name };
                _context.Tags.Add(tag);
                await _context.SaveChangesAsync();
            }

            // прикрепление файла
            var c = await _context.Contracts.Include(c2 => c2.Tags).FirstOrDefaultAsync(c2 => c2.Id == id);
            if (!c.Tags.Any(t => t.Id == tag.Id))
            {
                c.Tags.Add(tag);
                await _context.SaveChangesAsync();
            }

            return Ok(new { tagId = tag.Id, tagName = tag.Name });
        }

        [Authorize]
        [HttpDelete("{id}/tags/{tagId}")]
        public async Task<IActionResult> RemoveTag(int id, int tagId)
        {
            var c = await _context.Contracts.Include(c2 => c2.Tags).FirstOrDefaultAsync(c2 => c2.Id == id);
            if (c == null) return NotFound();

            // Проверка доступа
            var username = User.Identity?.Name;
            var isAdmin = User.IsInRole("admin");
            var isManager = User.IsInRole("manager");

            if (!isAdmin && !isManager)
            {
                var currentEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.Username == username);
                if (currentEmployee == null || c.ResponsibleEmployeeId != currentEmployee.Id)
                {
                    return Forbid();
                }
            }

            var tag = c.Tags.FirstOrDefault(t => t.Id == tagId);
            if (tag == null) return NotFound();
            c.Tags.Remove(tag);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [Authorize]
        [HttpGet("{id}/export")]
        public async Task<IActionResult> Export(int id, [FromQuery] string format = "csv")
        {
            var c = await _context.Contracts
                .Include(x => x.ResponsibleEmployee)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (c == null) return NotFound();

            // Проверка доступа
            var username = User.Identity?.Name;
            var isAdmin = User.IsInRole("admin");
            var isManager = User.IsInRole("manager");

            if (!isAdmin && !isManager)
            {
                var currentEmployee = await _context.Employees.FirstOrDefaultAsync(e => e.Username == username);
                if (currentEmployee == null || c.ResponsibleEmployeeId != currentEmployee.Id)
                {
                    return Forbid();
                }
            }

            var files = await _context.UserFiles.Where(f => f.ContractId == id).ToListAsync();
            var tags = await _context.Tags.Where(t => t.Contracts.Any(ct => ct.Id == id)).Select(t => t.Name).ToListAsync();

            // CSV экспорт
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Id,Title,ContractNumber,Status,StartDate,EndDate,Amount,Currency,ResponsibleEmployeeId,DepartmentId,Tags,Files");

            var fileNames = files.Select(f => f.DisplayName ?? string.Empty).ToArray();
            var tagList = tags ?? new System.Collections.Generic.List<string>();

            string[] fields = new string[] {
                c.Id.ToString(),
                EscapeCsv(c.Title),
                EscapeCsv(c.ContractNumber),
                EscapeCsv(c.Status),
                EscapeCsv(c.StartDate?.ToString("o")),
                EscapeCsv(c.EndDate?.ToString("o")),
                EscapeCsv(c.Amount?.ToString()),
                EscapeCsv(c.Currency),
                EscapeCsv(c.ResponsibleEmployeeId?.ToString()),
                EscapeCsv(c.DepartmentId?.ToString()),
                EscapeCsv(string.Join(";", tagList)),
                EscapeCsv(string.Join(";", fileNames))
            };

            sb.AppendLine(string.Join(",", fields.Select(f => '"' + (f ?? string.Empty) + '"')));

            var data = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(data, "text/csv", $"contract_{id}.csv");
        }

        private string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\"", "\"\"");
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var contract = await _context.Contracts
                .Include(x => x.ResponsibleEmployee)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (contract == null) return NotFound();

            _context.Contracts.Remove(contract);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
