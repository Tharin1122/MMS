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

## 🔴 BLOCKER ปัจจุบัน — ยังเทส endpoint ที่ต้อง auth ไม่ได้
ไม่มีทางได้ admin token อัตโนมัติ เพราะ:
1. dev login `Uowner_demo_001` → 401 (LineUserId ถูกเขียนทับด้วย LINE จริงตอนผูกบัญชี)
2. dev login `Utherapist_demo_001` → 401 (ไม่เคยถูกสร้าง — seeder ข้ามเพราะ tenant มาจาก SQL manual)
3. ไม่ทราบรหัสผ่าน owner (`ko1122541`)

**ต้องการจากผู้ใช้ (เลือก 1):**
- (ก) บอกรหัสผ่านของ `ko1122541` → ผมเทส admin API ครบ
- (ข) ให้ผมเพิ่ม endpoint `POST /auth/dev-token` (เฉพาะ ENV=Development) สร้าง token QA — แต่เป็น backdoor ไม่แนะนำใน prod
- (ค) สร้าง QA account ใหม่ผ่าน UI + ตั้ง temp password แล้วบอก user/pass มา

---

## Findings (บั๊ก/ข้อสังเกตที่เจอ)

### FINDING-01 · Severity: Medium · [OPEN]
**Dev login owner พังหลังผูก LINE จริง**
- ตอนผูก LINE จริงเข้า account owner → `link-line` set `user.LineUserId = realLineId` ทับ `Uowner_demo_001`
- ผล: dev login เดิมใช้ไม่ได้ + ถ้าจำรหัสผ่านไม่ได้ = ล็อกตัวเอง
- **แนะนำ:** seed บัญชี QA/owner แยกที่ไม่ถูกแตะ, หรือไม่ผูก LINE ทับ account ที่เป็น dev-login, หรือเก็บ dev identifier แยกจาก LineUserId

### FINDING-02 · Severity: Low · [OPEN]
**Therapist demo ไม่ถูกสร้าง**
- `SeedDemoTenantAsync` ข้ามทั้ง block ถ้ามี tenant อยู่แล้ว — แต่ตอน setup ผม seed tenant/owner ด้วย SQL มือ ทำให้ therapist demo ไม่เกิด
- **แนะนำ:** seeder ควร idempotent ราย entity (เช็คทีละ user) ไม่ใช่เช็คแค่ tenant

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
