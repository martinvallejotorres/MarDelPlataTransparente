using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ReclamosMDP.API.Models;


namespace ReclamosMDP.API.Data
{
    public class ReclamosDbContext : IdentityDbContext<ApplicationUser>
    {
        public ReclamosDbContext(
            DbContextOptions<ReclamosDbContext> options
        ) : base(options)
        {
        }


        public DbSet<Reclamo> Reclamos { get; set; }

        public DbSet<Apoyo> Apoyos { get; set; }

        public DbSet<HistorialEstado> HistorialEstados { get; set; }

        public DbSet<ObraPublica> ObrasPublicas { get; set; }

        public DbSet<ObraTramo> ObrasTramos { get; set; }

        public DbSet<ObrasAnioSincronizacion> ObrasAniosSincronizados { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);


            builder.Entity<Apoyo>()
                .HasOne(a => a.Reclamo)
                .WithMany(r => r.ApoyosUsuarios)
                .HasForeignKey(a => a.ReclamoId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Apoyo>()
                .HasIndex(a => new { a.ReclamoId, a.UsuarioId })
                .IsUnique();



            builder.Entity<Apoyo>()
                .HasOne(a => a.Usuario)
                .WithMany()
                .HasForeignKey(a => a.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<HistorialEstado>()
                .HasOne(h => h.Reclamo)
                .WithMany(r => r.HistorialEstados)
                .HasForeignKey(h => h.ReclamoId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<HistorialEstado>()
                .HasOne(h => h.Usuario)
                .WithMany()
                .HasForeignKey(h => h.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ObraPublica>()
                .HasIndex(o => o.AnioFuente);

            builder.Entity<ObraPublica>()
                .HasMany(o => o.Tramos)
                .WithOne(t => t.Obra)
                .HasForeignKey(t => t.ObraEventoId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ObraTramo>()
                .HasIndex(t => new { t.ObraEventoId, t.Calle, t.Desde, t.Hasta })
                .IsUnique();

        }
    }
}
