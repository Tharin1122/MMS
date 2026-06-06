using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Domain.Entities;

namespace MMS.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(x => x.SubTotal).HasPrecision(10, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(10, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(10, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(10, 2);
        builder.Property(x => x.ChangeAmount).HasPrecision(10, 2);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Booking)
            .WithOne(x => x.Payment)
            .HasForeignKey<Payment>(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WalkIn)
            .WithOne(x => x.Payment)
            .HasForeignKey<Payment>(x => x.WalkInId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}