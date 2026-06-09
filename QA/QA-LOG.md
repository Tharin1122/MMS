# MMS — QA Test Log (resumable)

> ไฟล์นี้บันทึกความคืบหน้าการทดสอบแบบต่อเนื่อง — ถ้า session ขาด ให้กลับมาอ่านไฟล์นี้เพื่อรู้ว่าเทสถึงไหน
> Test environment: PROD — Frontend `https://mms-tharin.vercel.app` / Backend `https://mms-api-25xj.onrender.com/api`
> ผู้ทดสอบ: Claude (สวมบท Senior QA) · เริ่ม 2026-06-10

---

## วิธีรันเทส (สำหรับ resume)
- Backend API: PowerShell `Invoke-RestMethod` ยิงตรงไป Render (cold start ~50s ครั้งแรก)
- ต้องมี **owner access token** ก่อน ถึงจะเทส endpoint ที่ต้อง auth ได้
- ได้ token โดย: `POST /auth/line-login {lineUserId}` (dev) **หรือ** `POST /auth/login {username,password}`
- ผลลัพธ์บันทึกใน `QA-TestPlan.csv` (เปิดด้วย Excel)

---

## ✅ UNBLOCKED — ได้ owner token แล้ว (ผู้ใช้ให้รหัส ko1122541)
รัน backend API test suite ครบ: **PASS 15 / FAIL 1** (ดู QA-TestPlan.csv)

### สรุปผล Phase A (Owner token — backend API)
- ✅ Auth: login user/pass, /me, เปลี่ยนรหัสต้องใส่รหัสเดิม, username ล็อกหลังตั้ง — ผ่านหมด
- ✅ User CRUD: สร้าง/บล็อก/ปลดบล็อก/ลบ/ตั้งรหัสชั่วคราว — ผ่าน + กันบล็อกตัวเอง (400)
- ✅ Permission: dedupe code + แสดงชื่อ user + กันแก้สิทธิ์ตัวเอง (403) — ผ่าน
- ✅ LINE: link-token + link-status pending — ผ่าน
- ❌ USER-01: role "Owner" ซ้ำใน DB (มี 2 ตัว) → **FINDING-03** (แก้ dedup ที่ API แล้ว)

### Phase B ที่เหลือ (ต้อง therapist token)
- USER-03 (non-owner สร้าง Owner→403), PERM-04, AUTHZ-01 (non-privileged เข้า /user→403)
- รอ deploy seeder (สร้าง therapist demo) → เรียก /auth/seed → dev login therapist → รัน

---

## Findings (บั๊ก/ข้อสังเกตที่เจอ)

### FINDING-01 · Severity: Medium · [OPEN]
**Dev login owner พังหลังผูก LINE จริง**
- ตอนผูก LINE จริงเข้า account owner → `link-line` set `user.LineUserId = realLineId` ทับ `Uowner_demo_001`
- ผล: dev login เดิมใช้ไม่ได้ + ถ้าจำรหัสผ่านไม่ได้ = ล็อกตัวเอง
- **แนะนำ:** seed บัญชี QA/owner แยกที่ไม่ถูกแตะ, หรือไม่ผูก LINE ทับ account ที่เป็น dev-login, หรือเก็บ dev identifier แยกจาก LineUserId

### FINDING-02 · Severity: Low · [FIXED]
**Therapist demo ไม่ถูกสร้าง**
- `SeedDemoTenantAsync` ข้ามทั้ง block ถ้ามี tenant อยู่แล้ว — ตอน setup seed ด้วย SQL มือ ทำให้ therapist demo ไม่เกิด
- **แก้แล้ว:** seeder idempotent ราย entity (`EnsureDemoUserAsync` เช็คทีละ user)

### FINDING-03 · Severity: Medium · [FIXED at API]
**role "Owner" ซ้ำใน DB (2 ตัว)**
- GET /user/roles คืน Owner 2 ครั้ง — เกิดจาก seed SQL + /auth/seed สร้าง Owner ซ้ำ
- **แก้แล้ว:** dedup by name ใน GetRoles (frontend filter Owner ออกอยู่แล้ว จึงไม่กระทบ dropdown)
- **ยังเหลือ:** DB ยังมีแถว Owner ซ้ำ (ไม่ลบเพราะ UserRole/RolePermission อาจอ้างถึง) — แนะนำ cleanup ด้วย SQL ที่ย้าย reference ไป canonical ก่อนลบ ถ้าต้องการความสะอาด

---

## Test Run History
| รอบ | เวลา | ทำอะไร | ผล |
|-----|------|--------|-----|
| 1 | 2026-06-10 | ยิง dev login owner + negative login + roles/list | dev login 401 (FINDING-01), AUTH-03 (wrong login→401) PASS, ที่เหลือ BLOCKED ไม่มี token |
| 2 | 2026-06-10 | ลอง therapist demo login | 401 (FINDING-02) |

---

## ถัดไป (Next steps เมื่อ resume)
1. ได้ owner token แล้ว → รัน USER-*, AUTH-05..09, PERM-*, LINK-* ตาม CSV
2. ติดตั้ง Chrome extension → เทส UI flow จริง (ฟอร์ม validation, modal, QR)
3. อัปเดต CSV ทุกครั้งหลังรัน + เขียน finding ใหม่ที่นี่
