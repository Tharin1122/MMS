using Microsoft.EntityFrameworkCore;
using MMS.Domain.Common;
using MMS.Domain.Entities;
using MMS.Infrastructure.Persistence.Interceptors;

namespace MMS.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    AuditInterceptor? auditInterceptor = null) : DbContext(options)
{
    // Phase 0 — Foundation
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpToken> OtpTokens => Set<OtpToken>();
    public DbSet<AccountLinkToken> AccountLinkTokens => Set<AccountLinkToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ActivityTimeline> ActivityTimelines => Set<ActivityTimeline>();
    public DbSet<NotificationQueue> NotificationQueues => Set<NotificationQueue>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
    public DbSet<CustomerPackage> CustomerPackages => Set<CustomerPackage>();

    // Phase 1 — Master Data
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomStatusHistory> RoomStatusHistories => Set<RoomStatusHistory>();
    public DbSet<Therapist> Therapists => Set<Therapist>();
    public DbSet<TherapistStatusHistory> TherapistStatusHistories => Set<TherapistStatusHistory>();
    public DbSet<TherapistSchedule> TherapistSchedules => Set<TherapistSchedule>();
    public DbSet<TherapistLeave> TherapistLeaves => Set<TherapistLeave>();
    public DbSet<TherapistBreak> TherapistBreaks => Set<TherapistBreak>();
    public DbSet<TherapistBlockTime> TherapistBlockTimes => Set<TherapistBlockTime>();
    public DbSet<TherapistService> TherapistServices => Set<TherapistService>();

    // Subscription / Plan
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();

    // Phase 3–6
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<WalkIn> WalkIns => Set<WalkIn>();
    public DbSet<WalkInItem> WalkInItems => Set<WalkInItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentItem> PaymentItems => Set<PaymentItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (auditInterceptor != null)
            optionsBuilder.AddInterceptors(auditInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            // เก็บเป็น UTC เสมอ → frontend แปลงเป็นเวลาท้องถิ่นตอนแสดง
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;

            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
