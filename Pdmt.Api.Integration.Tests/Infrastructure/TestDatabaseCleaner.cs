using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;

namespace Pdmt.Api.Integration.Tests.Infrastructure;

public static class TestDatabaseCleaner
{
    // Order matters: children before parents (FK constraints)
    public static async Task CleanAsync(AppDbContext db)
    {
        await db.EventTags.ExecuteDeleteAsync();
        await db.RefreshTokens.ExecuteDeleteAsync();
        await db.FailedLoginAttempts.ExecuteDeleteAsync();
        await db.Summaries.ExecuteDeleteAsync();
        await db.Events.ExecuteDeleteAsync();
        await db.Tags.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
    }
}
