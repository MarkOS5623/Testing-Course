using DataAccessor.Data;
using DataAccessor.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccessor.Repositories;

public class MeetingTasksRepository(MeetingFlowDbContext db)
{
    public async Task<List<MeetingTask>> GetAllAsync()
        => await db.MeetingTasks.OrderByDescending(t => t.CreatedAt).ToListAsync();

    public async Task<List<MeetingTask>> GetByMeetingAsync(Guid meetingId)
        => await db.MeetingTasks.Where(t => t.MeetingId == meetingId).OrderBy(t => t.CreatedAt).ToListAsync();

    public async Task<MeetingTask?> GetByIdAsync(Guid id)
        => await db.MeetingTasks.FindAsync(id);

    public async Task<MeetingTask> CreateAsync(MeetingTask item)
    {
        if (item.Id == Guid.Empty) item.Id = Guid.NewGuid();
        if (item.CreatedAt == default) item.CreatedAt = DateTimeOffset.UtcNow;
        db.MeetingTasks.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<MeetingTask?> UpdateAsync(Guid id, MeetingTask updated)
    {
        var existing = await db.MeetingTasks.FindAsync(id);
        if (existing is null) return null;

        existing.Title = updated.Title;
        existing.IsCompleted = updated.IsCompleted;
        existing.AssignedTo = updated.AssignedTo;
        if (updated.IsCompleted && existing.CompletedAt is null)
            existing.CompletedAt = DateTimeOffset.UtcNow;
        if (!updated.IsCompleted)
            existing.CompletedAt = null;

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var item = await db.MeetingTasks.FindAsync(id);
        if (item is null) return false;
        db.MeetingTasks.Remove(item);
        await db.SaveChangesAsync();
        return true;
    }
}
