using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Domain.Entities;

namespace MMS.Infrastructure.Persistence.Configurations;

public class WalkInItemConfiguration : IEntityTypeConfiguration<WalkInItem>
{
    public void Configure(EntityTypeBuilder<WalkInItem> builder)
    {
        builder.Property(x => x.Price).HasPrecision(10, 2);
        builder.Property(x => x.CommissionAmount).HasPrecision(10, 2);

        builder.HasOne(x => x.WalkIn)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.WalkInId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Therapist)
            .WithMany()
            .HasForeignKey(x => x.TherapistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Room)
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}