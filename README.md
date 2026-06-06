# MMS — Massage Management System

ระบบจัดการร้านนวด (Multi-tenant) พัฒนาด้วย ASP.NET Core 9 + MSSQL

---

## 🏗️ Architecture

```
MMS/
├── MMS.Api/              # ASP.NET Core Web API (Entry point)
│   ├── Controllers/      # API Controllers
│   ├── Properties/
│   ├── appsettings.json  # Config (อย่า commit ค่า production!)
│   └── Program.cs
├── MMS.Application/      # Business Logic / Use Cases
├── MMS.Domain/           # Entities, Enums, Interfaces
│   ├── Common/           # BaseEntity, TenantEntity
│   ├── Entities/         # 31 tables
│   └── Enums/
├── MMS.Infrastructure/   # EF Core, DbContext, Migrations, Services
│   └── Persistence/
│       ├── Auth/         # JwtService
│       ├── Configurations/
│       ├── Migrations/
│       ├── AppDbContext.cs
│       └── DbSeeder.cs
└── MMS.sln
```

---

## ⚙️ Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core 9 |
| ORM | Entity Framework Core |
| Database | SQL Server (MSSQL) |
| Auth | LINE Login + JWT Bearer |
| Realtime | SignalR *(Phase 0 Day 7)* |
| Background Jobs | Hangfire *(Phase 0 Day 6)* |
| Documentation | Swagger / OpenAPI |

---

## 🚀 Getting Started

### Prerequisites
- .NET 9 SDK
- SQL Server (local หรือ Docker)
- Visual Studio 2022 / Rider / VS Code

### Setup

```bash
# 1. Clone repo
git clone https://github.com/YOUR_USERNAME/MMS.git
cd MMS

# 2. Copy config และแก้ค่า connection string
cp MMS.Api/appsettings.example.json MMS.Api/appsettings.json
# แก้ไข appsettings.json ใส่ค่าของตัวเอง

# 3. Apply migrations
dotnet ef database update --project MMS.Infrastructure --startup-project MMS.Api

# 4. Seed ข้อมูลตัวอย่าง (Dev)
# POST /api/auth/seed

# 5. Run
dotnet run --project MMS.Api
```

API จะรันที่ `https://localhost:7xxx` และเปิด Swagger ที่ `/swagger`

---

## 🗺️ Development Roadmap

| Phase | งาน | Mandays | สถานะ |
|-------|-----|---------|-------|
| Pre-Dev | PRD, ERD, RBAC Design | ~3 วัน | ✅ เสร็จ |
| Phase 0 | Foundation (Auth, JWT, Tenant, RBAC) | 8 วัน | 🔵 กำลังทำ (Day 2/8) |
| Phase 1 | Master Data (Customer, Service, Room, Therapist) | 5 วัน | ⬜ รอ |
| Phase 2 | Schedule + Availability Engine | 5 วัน | ⬜ รอ |
| Phase 3 | Booking Engine | 6 วัน | ⬜ รอ |
| Phase 4 | Walk-In + Queue + Smart Wait | 5 วัน | ⬜ รอ |
| Phase 5 | Payment Engine | 3 วัน | ⬜ รอ |
| Phase 6 | Realtime Dashboard | 3 วัน | ⬜ รอ |
| Phase 7 | Notification (LINE OA) | 2 วัน | ⬜ รอ |
| Phase 8 | Reports | 4 วัน | ⬜ รอ |

---

## 🔐 Authentication

ระบบใช้ **LINE Login** เป็น Identity Provider:

1. LIFF ส่ง `LineUserId` มาที่ `POST /api/auth/line-login`
2. Server ตรวจสอบ User ใน DB และ return **JWT Token**
3. Client ใช้ JWT ใน `Authorization: Bearer <token>` header

### Demo Login (Dev only)
```json
POST /api/auth/line-login
{ "lineUserId": "Utherapist_demo_001" }
```

---

## 📝 Environment Variables

ดู `appsettings.example.json` สำหรับ config ที่ต้องใส่:
- `ConnectionStrings:DefaultConnection` — SQL Server connection string
- `Jwt:Key` — Secret key สำหรับ sign JWT (ต้องยาวอย่างน้อย 32 chars)
- `Line:ChannelId` / `Line:ChannelSecret` — จาก LINE Developers Console

> ⚠️ **ห้าม commit** `appsettings.json` ที่มีค่าจริงขึ้น Git เด็ดขาด

---

## 👤 Developer

**Tharin** — MMS Project Lead
