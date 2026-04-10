using FairQueueService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FairQueueService.Infrastructure.Persistence;

public sealed class FairQueueDbContext : DbContext
{
    public FairQueueDbContext(DbContextOptions<FairQueueDbContext> options) : base(options) { }

    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<QueueEntry>(e =>
        {
            e.ToTable("fair_queue");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.EventId).HasColumnName("event_id").IsRequired();
            e.Property(x => x.TicketId).HasColumnName("ticket_id").IsRequired();
            e.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            e.Property(x => x.Position).HasColumnName("position").IsRequired();
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion(s => s.ToString().ToLowerInvariant(),
                               s => Enum.Parse<QueueStatus>(s, true))
                .HasMaxLength(20)
                .IsRequired();
            e.Property(x => x.EnteredAt).HasColumnName("entered_at").IsRequired();
            e.Property(x => x.TurnStartsAt).HasColumnName("turn_started_at");
            e.Property(x => x.TurnExpiresAt).HasColumnName("turn_expires_at");

            e.HasIndex(x => new { x.TicketId, x.UserId }).IsUnique().HasDatabaseName("uq_queue_user_ticket");
            e.HasIndex(x => new { x.TicketId, x.Position }).IsUnique().HasDatabaseName("uq_queue_position");
            e.HasIndex(x => new { x.TicketId, x.Status, x.Position }).HasDatabaseName("idx_fq_ticket_status_pos");
        });
    }
}
