using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SchoolAdmission.Api;

public class AdmissionService(AppDbContext db, HoldOptions options)
{
    private static string NewId(string prefix) => prefix + Guid.NewGuid().ToString("N")[..12];

    // A HELD hold whose time has passed is EXPIRED even if the sweeper hasn't run yet.
    public static string EffectiveStatus(Hold h, DateTime now) =>
        h.Status == HoldStatus.Held && h.ExpiresAt <= now ? HoldStatus.Expired : h.Status;

    // ---------- Classes ----------

    public async Task<SchoolClass> CreateClassAsync(string? name, int? totalSeats)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
            throw new ApiException(400, "Class name is required.");
        if (name.Length > 200)
            throw new ApiException(400, "Class name is too long (max 200 characters).");
        if (totalSeats is null || totalSeats < 1 || totalSeats > 100_000)
            throw new ApiException(400, "Total seats must be a whole number between 1 and 100000.");

        var c = new SchoolClass
        {
            Id = NewId("c_"),
            Name = name,
            TotalSeats = totalSeats.Value,
            CreatedAt = DateTime.UtcNow
        };
        db.Classes.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    public async Task<List<ClassView>> ListClassesAsync()
    {
        var now = DateTime.UtcNow;
        var classes = await db.Classes.AsNoTracking()
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync();

        var rows = await db.Holds.AsNoTracking()
            .Where(h => h.Status == HoldStatus.Confirmed ||
                        (h.Status == HoldStatus.Held && h.ExpiresAt > now))
            .GroupBy(h => new { h.ClassId, h.Status })
            .Select(g => new { g.Key.ClassId, g.Key.Status, N = g.Count() })
            .ToListAsync();

        return classes.Select(c =>
        {
            var held = rows.Where(r => r.ClassId == c.Id && r.Status == HoldStatus.Held).Sum(r => r.N);
            var conf = rows.Where(r => r.ClassId == c.Id && r.Status == HoldStatus.Confirmed).Sum(r => r.N);
            return new ClassView(c.Id, c.Name, c.TotalSeats, held, conf, c.TotalSeats - held - conf);
        }).ToList();
    }

    public async Task<ClassView> GetClassAsync(string classId)
    {
        var c = await db.Classes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == classId)
            ?? throw new ApiException(404, "Class not found.");
        var (held, conf) = await CountsAsync(classId, DateTime.UtcNow);
        return new ClassView(c.Id, c.Name, c.TotalSeats, held, conf, c.TotalSeats - held - conf);
    }

    // One single SQL statement = one consistent snapshot of both numbers.
    private async Task<(int held, int confirmed)> CountsAsync(string classId, DateTime now)
    {
        var rows = await db.Holds.AsNoTracking()
            .Where(h => h.ClassId == classId &&
                        (h.Status == HoldStatus.Confirmed ||
                         (h.Status == HoldStatus.Held && h.ExpiresAt > now)))
            .GroupBy(h => h.Status)
            .Select(g => new { Status = g.Key, N = g.Count() })
            .ToListAsync();

        return (rows.Where(r => r.Status == HoldStatus.Held).Sum(r => r.N),
                rows.Where(r => r.Status == HoldStatus.Confirmed).Sum(r => r.N));
    }

    // ---------- Holds ----------

    public async Task<Hold> ApplyAsync(string classId, string studentId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        // Lock the class row. Every concurrent apply for this class waits here,
        // in this process or any other process sharing the same database.
        var cls = (await db.Classes
                .FromSql($"SELECT * FROM classes WHERE id = {classId} FOR UPDATE")
                .ToListAsync()).FirstOrDefault()
            ?? throw new ApiException(404, "Class not found.");

        var now = DateTime.UtcNow;

        // Clear this student's own stale holds so they don't block the unique index.
        await db.Holds
            .Where(h => h.ClassId == classId && h.StudentId == studentId &&
                        h.Status == HoldStatus.Held && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.Expired));

        var alreadyActive = await db.Holds.AnyAsync(h =>
            h.ClassId == classId && h.StudentId == studentId &&
            (h.Status == HoldStatus.Confirmed ||
             (h.Status == HoldStatus.Held && h.ExpiresAt > now)));
        if (alreadyActive)
            throw new ApiException(409, "You already have an active application for this class.");

        var (held, confirmed) = await CountsAsync(classId, now);
        if (held + confirmed >= cls.TotalSeats)
            throw new ApiException(409, "Sorry, no seats are available in this class.");

        var hold = new Hold
        {
            Id = NewId("h_"),
            ClassId = classId,
            StudentId = studentId,
            Status = HoldStatus.Held,
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(options.DurationSeconds)
        };
        db.Holds.Add(hold);

        try
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new ApiException(409, "You already have an active application for this class.");
        }
        return hold;
    }

    public async Task<(Hold hold, string status)> GetHoldAsync(string holdId)
    {
        var h = await db.Holds.AsNoTracking().FirstOrDefaultAsync(x => x.Id == holdId)
            ?? throw new ApiException(404, "Application not found.");
        return (h, EffectiveStatus(h, DateTime.UtcNow));
    }

    public async Task<Hold> ConfirmAsync(string holdId, string studentId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        // Lock the hold row, so parallel confirms run one at a time.
        var hold = await LockHoldAsync(holdId)
            ?? throw new ApiException(404, "Application not found.");
        if (hold.StudentId != studentId)
            throw new ApiException(403, "This application belongs to a different student.");

        // Idempotent: second and later confirms change nothing.
        if (hold.Status == HoldStatus.Confirmed) return hold;

        var now = DateTime.UtcNow;
        if (hold.Status == HoldStatus.Held && hold.ExpiresAt > now)
        {
            hold.Status = HoldStatus.Confirmed;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return hold;
        }

        if (hold.Status == HoldStatus.Held)   // overdue but not swept yet
        {
            hold.Status = HoldStatus.Expired;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        throw new ApiException(409, hold.Status == HoldStatus.Cancelled
            ? "This application was cancelled and cannot be confirmed."
            : "This application has expired. Please apply again.");
    }

    public async Task CancelAsync(string holdId, string studentId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var hold = await LockHoldAsync(holdId)
            ?? throw new ApiException(404, "Application not found.");
        if (hold.StudentId != studentId)
            throw new ApiException(403, "This application belongs to a different student.");

        if (hold.Status == HoldStatus.Cancelled) return;   // idempotent
        if (hold.Status == HoldStatus.Confirmed)
            throw new ApiException(409, "A confirmed admission cannot be cancelled.");

        var now = DateTime.UtcNow;
        if (hold.Status == HoldStatus.Expired || hold.ExpiresAt <= now)
        {
            if (hold.Status == HoldStatus.Held)
            {
                hold.Status = HoldStatus.Expired;
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            throw new ApiException(409, "This application has already expired.");
        }

        hold.Status = HoldStatus.Cancelled;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<List<object>> MyHoldsAsync(string studentId)
    {
        var now = DateTime.UtcNow;
        var rows = await (from h in db.Holds.AsNoTracking()
                          join c in db.Classes on h.ClassId equals c.Id
                          where h.StudentId == studentId
                          orderby h.CreatedAt descending
                          select new { h, c.Name }).ToListAsync();

        return rows.Select(r => (object)new
        {
            holdId = r.h.Id,
            classId = r.h.ClassId,
            className = r.Name,
            status = EffectiveStatus(r.h, now),
            expiresAt = r.h.ExpiresAt
        }).ToList();
    }

    private async Task<Hold?> LockHoldAsync(string holdId) =>
        (await db.Holds.FromSql($"SELECT * FROM holds WHERE id = {holdId} FOR UPDATE")
            .ToListAsync()).FirstOrDefault();
}