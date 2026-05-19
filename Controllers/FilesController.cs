using System;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Authorization;
using BackEnd.DbContexts;
using BackEnd.Models.DTO;
using BackEnd.Models.Employees;
using BackEnd.Models.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BackEnd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class FilesController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationContext _context;

        public FilesController(IWebHostEnvironment env, ApplicationContext context)
        {
            _env = env;
            _context = context;
        }

        // List files: if employeeId not provided - admin sees all, non-admin sees own files
        [Authorize]
        [HttpGet("list")]
        public async Task<IActionResult> ListFiles([FromQuery] int? employeeId)
        {
            try
            {
                var username = User.Identity?.Name;

                // If no employeeId provided
                if (!employeeId.HasValue || employeeId.Value == 0)
                {
                    if (User.IsInRole("admin"))
                    {
                        var all = await _context.UserFiles.OrderByDescending(f => f.UploadDate).ToListAsync();
                        return Ok(all.Select(f => new { 
                            f.Id, 
                            f.DisplayName, 
                            f.SystemName,
                            f.EmployeeId,
                            f.ContractId,
                            f.UploadDate 
                        }));
                    }

                    if (string.IsNullOrEmpty(username))
                        return Forbid();

                    var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Username == username);
                    if (emp == null)
                        return NotFound(new { error = "Employee not found" });

                    var list = await _context.UserFiles.Where(f => f.EmployeeId == emp.Id).OrderByDescending(f => f.UploadDate).ToListAsync();
                    return Ok(list.Select(f => new { 
                        f.Id, 
                        f.DisplayName, 
                        f.SystemName,
                        f.EmployeeId,
                        f.ContractId,
                        f.UploadDate 
                    }));
                }

                // employeeId provided
                var target = await _context.Employees.FindAsync(employeeId.Value);
                if (target == null)
                    return NotFound(new { error = "Employee not found" });

                if (!User.IsInRole("admin"))
                {
                    if (string.IsNullOrEmpty(username)) return Forbid();
                    var current = await _context.Employees.FirstOrDefaultAsync(e => e.Username == username);
                    if (current == null || current.Id != target.Id) return Forbid();
                }

                var files = await _context.UserFiles.Where(f => f.EmployeeId == employeeId.Value).OrderByDescending(f => f.UploadDate).ToListAsync();
                return Ok(files.Select(f => new { 
                    f.Id, 
                    f.DisplayName, 
                    f.SystemName,
                    f.EmployeeId,
                    f.ContractId,
                    f.UploadDate 
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Multipart/form-data upload: accept any file type via IFormFile
        [Authorize]
        [HttpPost("upload-multipart")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(200_000_000)] // 200 MB
        [RequestFormLimits(MultipartBodyLengthLimit = 200_000_000)]
        public async Task<IActionResult> UploadFileMultipart([FromForm] IFormFile file, [FromForm] int? employeeId, [FromForm] int? contractId)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "No file provided" });

                int targetEmployeeId = employeeId ?? 0;

                // Если employeeId не передан — определяем по токену
                if (targetEmployeeId == 0)
                {
                    var username = User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Unauthorized(new { error = "Failed to determine user from token" });

                    var empByUser = await _context.Employees
                        .FirstOrDefaultAsync(e => !string.IsNullOrEmpty(e.Username) && e.Username == username);

                    if (empByUser == null)
                    {
                        // Try to create employee for this user if it doesn't exist
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == username);
                        if (user == null)
                            return Unauthorized(new { error = "User not found in database" });

                        var newEmployee = new Employee
                        {
                            FirstName = user.FullName ?? username,
                            LastName = string.Empty,
                            Patronymic = string.Empty,
                            Email = user.Email ?? string.Empty,
                            PhoneNumber = string.Empty,
                            BirthDate = DateTime.MinValue,
                            Username = username,
                            DepartmentId = null
                        };
                        _context.Employees.Add(newEmployee);
                        await _context.SaveChangesAsync();
                        targetEmployeeId = newEmployee.Id;
                    }
                    else
                    {
                        targetEmployeeId = empByUser.Id;
                    }
                }
                else
                {
                    var emp = await _context.Employees.FindAsync(targetEmployeeId);
                    if (emp == null)
                        return NotFound(new { error = "Employee not found" });

                    if (!User.IsInRole("admin"))
                    {
                        var username = User.Identity?.Name;
                        if (emp.Username != username)
                            return Forbid();
                    }
                }

                string uploadsFolder = Path.Combine(_env.ContentRootPath, "filesStorage");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string extension = Path.GetExtension(file.FileName);
                string systemName = Guid.NewGuid().ToString() + extension;
                string filePath = Path.Combine(uploadsFolder, systemName);

                using (var stream = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                var userFile = new UserFile
                {
                    SystemName = systemName,
                    DisplayName = file.FileName,
                    EmployeeId = targetEmployeeId,
                    UploadDate = DateTime.UtcNow
                };

                // If contractId provided, validate and assign
                if (contractId.HasValue && contractId.Value > 0)
                {
                    var contract = await _context.Contracts.FindAsync(contractId.Value);
                    if (contract != null)
                    {
                        // Only admin/manager or owner can attach
                        if (!User.IsInRole("admin") && !User.IsInRole("manager"))
                        {
                            var username = User.Identity?.Name;
                            var owner = await _context.Employees.FindAsync(contract.ResponsibleEmployeeId);
                            if (owner == null || owner.Username != username)
                                return Forbid();
                        }

                        userFile.ContractId = contract.Id;
                    }
                }

                _context.UserFiles.Add(userFile);
                await _context.SaveChangesAsync();

                // If file was attached to a contract, add history entry
                if (userFile.ContractId.HasValue)
                {
                    _context.ContractHistories.Add(new ContractHistory
                    {
                        ContractId = userFile.ContractId.Value,
                        Action = "FileAttached",
                        PerformedBy = User.Identity?.Name,
                        Details = $"File '{userFile.DisplayName}' (id:{userFile.Id}) attached"
                    });
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "File uploaded successfully", fileId = userFile.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromBody] FileRequestDto request)
        {
            try
            {
                int targetEmployeeId = request.EmployeeId;

                // Если employeeId не передан — определяем сотрудника по текущему пользователю (по username в токене)
                if (targetEmployeeId == 0)
                {
                    var username = User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Unauthorized(new { error = "Failed to determine user from token" });

                    var empByUser = await _context.Employees
                        .FirstOrDefaultAsync(e => !string.IsNullOrEmpty(e.Username) && e.Username == username);

                    if (empByUser == null)
                    {
                        // Try to create employee for this user if it doesn't exist
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == username);
                        if (user == null)
                            return Unauthorized(new { error = "User not found in database" });

                        var newEmployee = new Employee
                        {
                            FirstName = user.FullName ?? username,
                            LastName = string.Empty,
                            Patronymic = string.Empty,
                            Email = user.Email ?? string.Empty,
                            PhoneNumber = string.Empty,
                            BirthDate = DateTime.MinValue,
                            Username = username,
                            DepartmentId = null
                        };
                        _context.Employees.Add(newEmployee);
                        await _context.SaveChangesAsync();
                        targetEmployeeId = newEmployee.Id;
                    }
                    else
                    {
                        targetEmployeeId = empByUser.Id;
                    }
                }
                else
                {
                    // If employeeId is specified, verify that such a record exists
                    var emp = await _context.Employees.FindAsync(targetEmployeeId);
                    if (emp == null)
                        return NotFound(new { error = "Employee not found" });

                    // If current user is not admin, forbid uploading on behalf of another employee
                    if (!User.IsInRole("admin"))
                    {
                        var username = User.Identity?.Name;
                        if (emp.Username != username)
                            return Forbid();
                    }
                }

                string uploadsFolder = Path.Combine(_env.ContentRootPath, "filesStorage");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string extension = Path.GetExtension(request.FileName);
                string systemName = Guid.NewGuid().ToString() + extension;
                string filePath = Path.Combine(uploadsFolder, systemName);

                // Normalize Base64 input: strip data URI prefix if present and remove whitespace
                if (string.IsNullOrWhiteSpace(request.Base64Content))
                    return BadRequest(new { error = "Base64 content is empty" });

                string base64 = request.Base64Content.Trim();
                // If data URI like "data:...;base64,<data>", strip prefix
                int commaIndex = base64.IndexOf(',');
                if (commaIndex >= 0 && base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    base64 = base64.Substring(commaIndex + 1);
                }

                // Remove whitespace/newlines which may be present when copy-pasting
                base64 = base64
                    .Replace("\r", string.Empty)
                    .Replace("\n", string.Empty)
                    .Replace("\t", string.Empty)
                    .Replace(" ", string.Empty);

                byte[] fileBytes;
                try
                {
                    fileBytes = Convert.FromBase64String(base64);
                }
                catch (FormatException)
                {
                    return BadRequest(new { error = "Invalid Base64: check the Base64Content format" });
                }
                await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

                var userFile = new UserFile
                {
                    SystemName = systemName,
                    DisplayName = request.FileName,
                    EmployeeId = targetEmployeeId,
                    UploadDate = DateTime.UtcNow
                };

                // If contractId provided, validate and assign
                if (request.ContractId.HasValue && request.ContractId.Value > 0)
                {
                    var contract = await _context.Contracts.FindAsync(request.ContractId.Value);
                    if (contract != null)
                    {
                        // Only admin/manager or owner can attach
                        if (!User.IsInRole("admin") && !User.IsInRole("manager"))
                        {
                            var username = User.Identity?.Name;
                            var owner = await _context.Employees.FindAsync(contract.ResponsibleEmployeeId);
                            if (owner == null || owner.Username != username)
                                return Forbid();
                        }

                        userFile.ContractId = contract.Id;
                    }
                }

                _context.UserFiles.Add(userFile);
                await _context.SaveChangesAsync();

                // If file was attached to a contract, add history entry
                if (userFile.ContractId.HasValue)
                {
                    _context.ContractHistories.Add(new ContractHistory
                    {
                        ContractId = userFile.ContractId.Value,
                        Action = "FileAttached",
                        PerformedBy = User.Identity?.Name,
                        Details = $"File '{userFile.DisplayName}' (id:{userFile.Id}) attached"
                    });
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "File successfully uploaded", fileId = userFile.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile([FromQuery] int? fileId)
        {
            try
            {
                if (!fileId.HasValue || fileId.Value <= 0)
                    return BadRequest(new { error = "File ID is invalid or missing" });

                var userFile = await _context.UserFiles.FindAsync(fileId.Value);
                if (userFile == null)
                    return NotFound(new { error = "File not found in database" });

                // Allow access if user is admin/manager
                // or if they are the owner of the file OR responsible for the contract with this file
                if (!User.IsInRole("admin") && !User.IsInRole("manager"))
                {
                    var username = User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Forbid();

                    var currentEmployee = await _context.Employees
                        .FirstOrDefaultAsync(e => !string.IsNullOrEmpty(e.Username) && e.Username == username);

                    if (currentEmployee == null)
                        return Forbid();

                    // Check if user is owner of file
                    var owner = await _context.Employees.FindAsync(userFile.EmployeeId);
                    bool isOwner = owner != null && owner.Username == username;

                    // Check if user is responsible for contract containing this file
                    bool isContractResponsible = false;
                    if (userFile.ContractId.HasValue)
                    {
                        var contract = await _context.Contracts
                            .Include(x => x.ResponsibleEmployee)
                            .FirstOrDefaultAsync(x => x.Id == userFile.ContractId.Value);
                        isContractResponsible = contract != null && contract.ResponsibleEmployeeId == currentEmployee.Id;
                    }

                    if (!isOwner && !isContractResponsible)
                        return Forbid();
                }

                string filePath = Path.Combine(_env.ContentRootPath, "filesStorage", userFile.SystemName);
                if (!System.IO.File.Exists(filePath))
                    return NotFound(new { error = "Physical file not found" });

                byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(fileBytes, "application/octet-stream", userFile.DisplayName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteFile([FromBody] DownloadFileRequestDto request)
        {
            try
            {
                var userFile = await _context.UserFiles.FindAsync(request.FileId);
                if (userFile == null)
                    return NotFound(new { error = "File not found in database" });

                // Check permissions: admin can delete any file
                // User can delete only their own file or file from their contract
                if (!User.IsInRole("admin"))
                {
                    var username = User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                        return Forbid();

                    var currentEmployee = await _context.Employees
                        .FirstOrDefaultAsync(e => !string.IsNullOrEmpty(e.Username) && e.Username == username);

                    if (currentEmployee == null)
                        return Forbid();

                    // Check if user is owner of file
                    bool isOwner = userFile.EmployeeId == currentEmployee.Id;

                    // Check if user is responsible for contract containing this file
                    bool isContractResponsible = false;
                    if (userFile.ContractId.HasValue)
                    {
                        var contract = await _context.Contracts
                            .Include(x => x.ResponsibleEmployee)
                            .FirstOrDefaultAsync(x => x.Id == userFile.ContractId.Value);
                        isContractResponsible = contract != null && contract.ResponsibleEmployeeId == currentEmployee.Id;
                    }

                    if (!isOwner && !isContractResponsible)
                        return Forbid();
                }

                string filePath = Path.Combine(_env.ContentRootPath, "filesStorage", userFile.SystemName);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                // Если файл был прикреплен к контракту, добавляем запись в историю
                if (userFile.ContractId.HasValue)
                {
                    _context.ContractHistories.Add(new ContractHistory
                    {
                        ContractId = userFile.ContractId.Value,
                        Action = "FileDeleted",
                        PerformedBy = User.Identity?.Name,
                        Details = $"Файл '{userFile.DisplayName}' (id:{userFile.Id}) удалён"
                    });
                }

                _context.UserFiles.Remove(userFile);
                await _context.SaveChangesAsync();

                return Ok(new { message = "File deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
