using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BackEnd.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace BackEnd.Services
{
    // Periodic background service that checks contracts for upcoming end date and creates reminders in ContractHistory
    public class NotificationsService : BackgroundService
    {
        private readonly ILogger<NotificationsService> _logger;
        private readonly IServiceProvider _services;
        private readonly TimeSpan _period = TimeSpan.FromHours(1); // run every hour

        public NotificationsService(ILogger<NotificationsService> logger, IServiceProvider services)
        {
            _logger = logger;
            _services = services;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationsService started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckContractsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in NotificationsService");
                }

                await Task.Delay(_period, stoppingToken);
            }
        }

        private async Task CheckContractsAsync(CancellationToken ct)
        {
            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

                var now = DateTime.UtcNow;
                var remindBefore = now.AddDays(30);

                var expiring = await db.Contracts
                    .Where(c => c.EndDate.HasValue && c.EndDate.Value > now && c.EndDate.Value <= remindBefore)
                    .ToListAsync(ct);

                foreach (var c in expiring)
                {
                    // check if a reminder already exists in history for this period
                    var exists = await db.ContractHistories.AnyAsync(h => h.ContractId == c.Id && h.Action == "ExpiryReminder" && h.PerformedAt > now.AddDays(-7), ct);
                    if (exists) continue;

                    db.ContractHistories.Add(new BackEnd.Models.Contracts.ContractHistory
                    {
                        ContractId = c.Id,
                        Action = "ExpiryReminder",
                        PerformedBy = "system",
                        Details = $"Contract {c.Id} expires on {c.EndDate.Value:yyyy-MM-dd}"
                    });
                }

                await db.SaveChangesAsync(ct);
            }
        }
    }
}
