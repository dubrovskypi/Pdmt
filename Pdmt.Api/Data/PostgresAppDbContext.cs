using Microsoft.EntityFrameworkCore;

namespace Pdmt.Api.Data
{
    public class PostgresAppDbContext : AppDbContext
    {
        public PostgresAppDbContext(DbContextOptions<PostgresAppDbContext> options) : base(options)
        {
        }
    }
}
