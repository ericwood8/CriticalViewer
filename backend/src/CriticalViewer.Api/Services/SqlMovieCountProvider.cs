using CriticalViewer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CriticalViewer.Api.Services;

// Reads dbo.Movies' row count from MySQL's InnoDB table statistics rather
// than running SELECT COUNT(*), which would force a full table scan.
// TABLE_ROWS is an approximation (InnoDB estimates it from index cardinality
// samples, refreshed by normal DML/ANALYZE TABLE) rather than an exact
// live count - acceptable here since the brief calls for this specifically
// as a fast/cheap check, not a source of truth. The projected column must
// be aliased "Value" for EF Core's scalar raw-SQL mapping (SqlQueryRaw<T>)
// to work.
public class SqlMovieCountProvider(AppDbContext db) : IMovieCountProvider
{
    public async Task<int> GetTotalMovieCountAsync()
    {
        // No matching row (e.g. the table were ever missing) yields an
        // empty result set rather than a NULL row, so both cases need
        // handling - unlike SQL Server's SUM(), which always returns
        // exactly one row (NULL when there's nothing to sum).
        var counts = await db.Database
            .SqlQueryRaw<long?>("""
                SELECT TABLE_ROWS AS Value
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Movies';
                """)
            .ToListAsync();

        return counts.Count > 0 ? (int)(counts[0] ?? 0) : 0;
    }
}
