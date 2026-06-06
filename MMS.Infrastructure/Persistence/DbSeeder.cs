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
        // ถ้ามี Tenant อยู่แล้วไม่ต้อง seed ซ้ำ
        if (await db.Tenants.Select(t => t.Id).AnyAsync())
            return;

        // สร้าง Tenant
        var tenant = new Tenant
        {
            Name = "ร้านนวดตัวอย่าง",
            Slug = "demo-spa",
            Phone = "02-000-0000",
            Status = Domain.Enums.TenantStatus.Active,
            PlanType = "Free",
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // สร้าง Branch
        var branch = new Branch
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

        // ดึง Role Owner
        var ownerRole = await db.Roles.FirstAsync(r => r.Name == "Owner");
        var therapistRole = await db.Roles.FirstAsync(r => r.Name == "Therapist");

        // สร้าง User Owner
        var owner = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            LineUserId = "Uowner_demo_001",   // ใช้ทดสอบ login
            DisplayName = "เจ้าของร้าน (Demo)",
            AuthProvider = Domain.Enums.AuthProvider.Line,
            IsActive = true,
            UserRoles = new List<UserRole>
        {
            new UserRole
            {
                RoleId = ownerRole.Id,
                BranchId = branch.Id,
                AssignedAt = DateTime.UtcNow,
            }
        }
        };
        db.Users.Add(owner);

        // สร้าง User Therapist ตัวอย่าง
        var therapistUser = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            LineUserId = "Utherapist_demo_001",  // ใช้ทดสอบ login
            DisplayName = "มิ้นท์ (Demo Therapist)",
            AuthProvider = Domain.Enums.AuthProvider.Line,
            IsActive = true,
            UserRoles = new List<UserRole>
        {
            new UserRole
            {
                RoleId = therapistRole.Id,
                BranchId = branch.Id,
                AssignedAt = DateTime.UtcNow,
            }
        }
        };
        db.Users.Add(therapistUser);

        await db.SaveChangesAsync();
    }



}