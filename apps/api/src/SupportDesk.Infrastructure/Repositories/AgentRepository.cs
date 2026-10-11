using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    /// <summary>Loads the whole aggregate, specializations included.</summary>
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>Loads all active agents with their specializations.</summary>
    public async Task<IReadOnlyList<Agent>> GetAllActiveAsync(CancellationToken ct) =>
        await db.Set<Agent>()
            .Include(a => a.Specializations)
            .Where(a => a.IsActive)
            .ToListAsync(ct);
}
