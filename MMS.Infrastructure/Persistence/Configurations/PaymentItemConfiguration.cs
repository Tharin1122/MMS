using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMS.Domain.Entities;

namespace MMS.Infrastructure.Persistence.Configurations;

public class PaymentItemConfiguration : IEntityTypeConfiguration<PaymentItem>
{
    public void Configure(EntityTypeBuilder<PaymentItem> builder)
    {
        builder.Property(x => x.UnitPrice).HasPrecision(10, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(10, 2);
        builder.Property(x => x.LineTotal).HasPrecision(10, 2);
        builder.Property(x => x.CommissionAmount).HasPrecision(10, 2);

        builder.HasOne(x => x.Payment)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}