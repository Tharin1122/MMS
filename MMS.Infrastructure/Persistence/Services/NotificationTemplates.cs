namespace MMS.Infrastructure.Persistence.Services;

public static class NotificationTemplates
{
    public static string BookingConfirmed(
        string customerName, string bookingNo,
        string date, string startTime, string serviceName) =>
        $"สวัสดีคุณ{customerName} 🙏\n" +
        $"ยืนยันการจองเรียบร้อยแล้วค่ะ\n\n" +
        $"📋 เลขที่: {bookingNo}\n" +
        $"📅 วันที่: {date}\n" +
        $"⏰ เวลา: {startTime} น.\n" +
        $"💆 บริการ: {serviceName}\n\n" +
        $"หากต้องการเปลี่ยนแปลงหรือยกเลิก กรุณาติดต่อเราล่วงหน้าค่ะ";

    public static string BookingReminder(
        string customerName, string bookingNo,
        string date, string startTime) =>
        $"แจ้งเตือนการจอง 📌\n\n" +
        $"สวัสดีคุณ{customerName}\n" +
        $"พรุ่งนี้คุณมีนัดกับเราค่ะ\n\n" +
        $"📋 เลขที่: {bookingNo}\n" +
        $"📅 วันที่: {date}\n" +
        $"⏰ เวลา: {startTime} น.\n\n" +
        $"รอต้อนรับค่ะ 😊";

    public static string BookingCancelled(
        string customerName, string bookingNo, string? reason) =>
        $"สวัสดีคุณ{customerName}\n" +
        $"การจองหมายเลข {bookingNo} ถูกยกเลิกแล้วค่ะ" +
        (string.IsNullOrWhiteSpace(reason) ? "" : $"\n\nเหตุผล: {reason}") +
        $"\n\nหากต้องการจองใหม่ สามารถติดต่อเราได้เลยนะคะ 🙏";

    public static string BookingCompleted(
        string customerName, string serviceName) =>
        $"ขอบคุณคุณ{customerName} มากนะคะ 🙏\n" +
        $"หวังว่าจะได้รับบริการ {serviceName} ถูกใจนะคะ\n\n" +
        $"ยินดีต้อนรับครั้งหน้าเสมอค่ะ 😊";

    public static string WalkInQueueConfirmed(
        string customerName, string queueNo, int estimatedWaitMins) =>
        $"สวัสดีคุณ{customerName} 🙏\n" +
        $"รับคิวเรียบร้อยแล้วค่ะ\n\n" +
        $"🎫 หมายเลขคิว: {queueNo}\n" +
        $"⏳ รอประมาณ: {estimatedWaitMins} นาที\n\n" +
        $"กรุณารอสักครู่นะคะ";

    public static string WalkInServiceStarted(
        string customerName, string therapistName, string serviceName) =>
        $"สวัสดีคุณ{customerName} 😊\n" +
        $"เริ่มให้บริการแล้วค่ะ\n\n" +
        $"💆 ช่าง: {therapistName}\n" +
        $"🛁 บริการ: {serviceName}";

    public static string TherapistNewAssignment(
        string therapistName, string customerName, string serviceName) =>
        $"สวัสดีคุณ{therapistName} 👋\n" +
        $"มีลูกค้าใหม่รอรับบริการค่ะ\n\n" +
        $"👤 ลูกค้า: {customerName}\n" +
        $"💆 บริการ: {serviceName}";

    public static string TherapistBookingReminder(
        string therapistName, string customerName,
        string date, string startTime, string serviceName) =>
        $"แจ้งเตือนนัดหมาย 📌\n\n" +
        $"สวัสดีคุณ{therapistName}\n" +
        $"พรุ่งนี้มีลูกค้านัดค่ะ\n\n" +
        $"👤 ลูกค้า: {customerName}\n" +
        $"📅 วันที่: {date}\n" +
        $"⏰ เวลา: {startTime} น.\n" +
        $"💆 บริการ: {serviceName}";
}

public static class NotificationEvents
{
    public const string BookingConfirmed = "BOOKING_CONFIRMED";
    public const string BookingReminder = "BOOKING_REMINDER";
    public const string BookingCancelled = "BOOKING_CANCELLED";
    public const string BookingCompleted = "BOOKING_COMPLETED";
    public const string WalkInQueued = "WALKIN_QUEUED";
    public const string WalkInStarted = "WALKIN_STARTED";
    public const string TherapistAssigned = "THERAPIST_ASSIGNED";
    public const string TherapistReminder = "THERAPIST_REMINDER";
}
