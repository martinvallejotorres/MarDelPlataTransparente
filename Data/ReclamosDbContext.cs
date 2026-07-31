using Microsoft.EntityFrameworkCore;
using ReclamosMDP.API.Models;

namespace ReclamosMDP.API.Data
{
    public class ReclamosDbContext : DbContext
    {

        public ReclamosDbContext(
            DbContextOptions<ReclamosDbContext> options
        ) : base(options)
        {

        }

        public DbSet<Reclamo> Reclamos { get; set; }

    }
}