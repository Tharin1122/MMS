using Microsoft.EntityFrameworkCore;
using MMS.Domain.Entities;

namespace MMS.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await SeedPermissionsAsync(db);
        await SeedRolesAsync(db);
        await SeedDemoTenantAsync(db); // เพิ่มบรรทัดนี้
        await db.SaveChangesAsync();
    }

    private static async Task SeedPermissionsAsync(AppDbContext db)
    {
        var permissions = new List<(string Code, string Group)>
    {
        ("DASHBOARD_VIEW", "Dashboard"),
        ("BOOKING_VIEW", "Booking"),
        ("BOOKING_CREATE", "Booking"),
        ("BOOKING_EDIT", "Booking"),
        ("BOOKING_CANCEL", "Booking"),
        ("WALKIN_VIEW", "WalkIn"),
        ("WALKIN_CREATE", "WalkIn"),
        ("WALKIN_ASSIGN", "WalkIn"),
        ("QUEUE_VIEW", "Queue"),
        ("QUEUE_MANAGE", "Queue"),
        ("THERAPIST_VIEW", "Therapist"),
        ("THERAPIST_CREATE", "Therapist"),
        ("THERAPIST_EDIT", "Therapist"),
        ("THERAPIST_DELETE", "Therapist"),
        ("THERAPIST_STATUS_CHANGE", "Therapist"),
        ("THERAPIST_SCHEDULE_VIEW", "Therapist"),
        ("THERAPIST_SCHEDULE_EDIT", "Therapist"),
        ("THERAPIST_LEAVE_MANAGE", "Therapist"),
        ("ROOM_VIEW", "Room"),
        ("ROOM_CREATE", "Room"),
        ("ROOM_EDIT", "Room"),
        ("ROOM_STATUS_CHANGE", "Room"),
        ("SERVICE_VIEW", "Service"),
        ("SERVICE_CREATE", "Service"),
        ("SERVICE_EDIT", "Service"),
        ("SERVICE_DELETE", "Service"),
        ("CUSTOMER_VIEW", "Customer"),
        ("CUSTOMER_CREATE", "Customer"),
        ("CUSTOMER_EDIT", "Customer"),
        ("PAYMENT_VIEW", "Payment"),
        ("PAYMENT_CREATE", "Payment"),
        ("PAYMENT_REFUND", "Payment"),
        ("REPORT_VIEW", "Report"),
        ("REPORT_EXPORT", "Report"),
        ("USER_VIEW", "User"),
        ("USER_CREATE", "User"),
        ("USER_EDIT", "User"),
        ("USER_ROLE_ASSIGN", "User"),
        ("BRANCH_VIEW", "Branch"),
        ("BRANCH_EDIT", "Branch"),
        ("SETTINGS_VIEW", "Settings"),
        ("SETTINGS_EDIT", "Settings"),
    };

        // ดึง code ที่มีอยู่แล้วมาเป็น HashSet
        var existingCodes = await db.Permissions
            .Select(p => p.Code)
            .ToListAsync();

        var existingSet = existingCodes.ToHashSet();

        foreach (var (code, group) in permissions)
        {
            if (!existingSet.Contains(code))
            {
                db.Permissions.Add(new Permission
                {
                    Code = code,
                    GroupName = group,
                    Description = code.Replace("_", " ")
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedRolesAsync(AppDbContext db)
    {
        var allPerms = await db.Permissions.ToListAsync();
        Permission Get(string code) => allPerms.First(p => p.Code == code);

        // ดึง role names ที่มีอยู่แล้ว
        var existingRoleNames = await db.Roles
            .Where(r => r.TenantId == null)
            .Select(r => r.Name)
            .ToListAsync();

        var existingSet = existingRoleNames.ToHashSet();

        var roles = new[]
        {
        new
        {
            Name = "Owner",
            Description = "เจ้าของร้าน — สิทธิ์ทุกอย่าง",
            Permissions = allPerms
        },
        new
        {
            Name = "Manager",
            Description = "ผู้จัดการสาขา",
            Permissions = allPerms
                .Where(p => p.Code != "SETTINGS_EDIT" && p.Code != "USER_ROLE_ASSIGN")
                .ToList()
        },
        new
        {
            Name = "Reception",
            Description = "พนักงานต้อนรับ",
            Permissions = new List<Permission>
            {
                Get("DASHBOARD_VIEW"),
                Get("BOOKING_VIEW"), Get("BOOKING_CREATE"), Get("BOOKING_EDIT"), Get("BOOKING_CANCEL"),
                Get("WALKIN_VIEW"), Get("WALKIN_CREATE"), Get("WALKIN_ASSIGN"),
                Get("QUEUE_VIEW"), Get("QUEUE_MANAGE"),
                Get("CUSTOMER_VIEW"), Get("CUSTOMER_CREATE"), Get("CUSTOMER_EDIT"),
                Get("THERAPIST_VIEW"), Get("THERAPIST_STATUS_CHANGE"), Get("THERAPIST_SCHEDULE_VIEW"),
                Get("ROOM_VIEW"), Get("ROOM_STATUS_CHANGE"),
                Get("SERVICE_VIEW"),
                Get("PAYMENT_VIEW"), Get("PAYMENT_CREATE"),
            }
        },
        new
        {
            Name = "Cashier",
            Description = "แคชเชียร์",
            Permissions = new List<Permission>
            {
                Get("DASHBOARD_VIEW"),
                Get("BOOKING_VIEW"),
                Get("WALKIN_VIEW"),
                Get("QUEUE_VIEW"),
                Get("CUSTOMER_VIEW"),
                Get("SERVICE_VIEW"),
                Get("PAYMENT_VIEW"), Get("PAYMENT_CREATE"), Get("PAYMENT_REFUND"),
                Get("REPORT_VIEW"),
            }
        },
        new
        {
            Name = "Therapist",
            Description = "หมอนวด",
            Permissions = new List<Permission>
            {
                Get("DASHBOARD_VIEW"),
                Get("BOOKING_VIEW"),
                Get("WALKIN_VIEW"), Get("WALKIN_CREATE"),
                Get("QUEUE_VIEW"), Get("QUEUE_MANAGE"),
                Get("THERAPIST_STATUS_CHANGE"),
                Get("THERAPIST_SCHEDULE_VIEW"),
                Get("CUSTOMER_VIEW"),
                Get("SERVICE_VIEW"),
            }
        },
    };

        foreach (var roleData in roles)
        {
            if (existingSet.Contains(roleData.Name))
                continue;

            var role = new Role
            {
                Name = roleData.Name,
                Description = roleData.Description,
                IsSystem = true,
                TenantId = null,
                RolePermissions = roleData.Permissions.Select(p => new RolePermission
                {
                    PermissionId = p.Id
                }).ToList()
            };

            db.Roles.Add(role);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedDemoTenantAsync(AppDbContext db)
    {
        // Idempotent ราย entity — ใช้ slug/code/lineUserId เป็น key (ปลอดภัยถ้ารันซ้ำ)
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == "demo-spa");
        if (tenant == null)
        {
            tenant = new Tenant
            {
                Name = "ร้านนวดตัวอย่าง",
                Slug = "demo-spa",
                Phone = "02-000-0000",
                Status = Domain.Enums.TenantStatus.Active,
                PlanType = "Free",
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.TenantId == tenant.Id && b.Code == "MAIN-01");
        if (branch == null)
        {
            branch = new Branch
            {
                TenantId = tenant.Id,
                Name = "สาขาหลัก",
                Code = "MAIN-01",
                OpenTime = new TimeOnly(10, 0),
                CloseTime = new TimeOnly(22, 0),
                IsActive = true,
            };
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
        }

        var ownerRole = await db.Roles.FirstAsync(r => r.Name == "Owner");
        var therapistRole = await db.Roles.FirstAsync(r => r.Name == "Therapist");

        // demo therapist สำหรับ dev login / ทดสอบ authz (idempotent)
        // หมายเหตุ: ไม่สร้าง owner demo เพราะ owner จริงผูก LINE แล้ว (กันบัญชี owner ซ้ำ)
        await EnsureDemoUserAsync(db, tenant.Id, branch.Id, "Utherapist_demo_001", "มิ้นท์ (Demo Therapist)", therapistRole.Id);
        _ = ownerRole;
    }

    private static async Task EnsureDemoUserAsync(
        AppDbContext db, Guid tenantId, Guid branchId, string lineUserId, string displayName, Guid roleId)
    {
        var exists = await db.Users.AnyAsync(u => u.LineUserId == lineUserId && u.DeletedAt == null);
        if (exists) return;

        db.Users.Add(new User
        {
            TenantId = tenantId,
            BranchId = branchId,
            LineUserId = lineUserId,
            DisplayName = displayName,
            AuthProvider = Domain.Enums.AuthProvider.Line,
            IsActive = true,
            UserRoles = new List<UserRole>
            {
                new() { RoleId = roleId, BranchId = branchId, AssignedAt = DateTime.UtcNow }
            }
        });
        await db.SaveChangesAsync();
    }



}