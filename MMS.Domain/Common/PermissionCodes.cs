namespace MMS.Domain.Common;

/// <summary>
/// รวม Permission Code ทั้งหมดเป็น constants เพื่อป้องกัน typo
/// </summary>
public static class PermissionCodes
{
    public const string DashboardView = "DASHBOARD_VIEW";

    public const string BookingView = "BOOKING_VIEW";
    public const string BookingCreate = "BOOKING_CREATE";
    public const string BookingEdit = "BOOKING_EDIT";
    public const string BookingCancel = "BOOKING_CANCEL";

    public const string WalkInView = "WALKIN_VIEW";
    public const string WalkInCreate = "WALKIN_CREATE";
    public const string WalkInAssign = "WALKIN_ASSIGN";

    public const string QueueView = "QUEUE_VIEW";
    public const string QueueManage = "QUEUE_MANAGE";

    public const string TherapistView = "THERAPIST_VIEW";
    public const string TherapistCreate = "THERAPIST_CREATE";
    public const string TherapistEdit = "THERAPIST_EDIT";
    public const string TherapistDelete = "THERAPIST_DELETE";
    public const string TherapistStatusChange = "THERAPIST_STATUS_CHANGE";
    public const string TherapistScheduleView = "THERAPIST_SCHEDULE_VIEW";
    public const string TherapistScheduleEdit = "THERAPIST_SCHEDULE_EDIT";
    public const string TherapistLeaveManage = "THERAPIST_LEAVE_MANAGE";

    public const string RoomView = "ROOM_VIEW";
    public const string RoomCreate = "ROOM_CREATE";
    public const string RoomEdit = "ROOM_EDIT";
    public const string RoomStatusChange = "ROOM_STATUS_CHANGE";

    public const string ServiceView = "SERVICE_VIEW";
    public const string ServiceCreate = "SERVICE_CREATE";
    public const string ServiceEdit = "SERVICE_EDIT";
    public const string ServiceDelete = "SERVICE_DELETE";

    public const string CustomerView = "CUSTOMER_VIEW";
    public const string CustomerCreate = "CUSTOMER_CREATE";
    public const string CustomerEdit = "CUSTOMER_EDIT";

    public const string PaymentView = "PAYMENT_VIEW";
    public const string PaymentCreate = "PAYMENT_CREATE";
    public const string PaymentRefund = "PAYMENT_REFUND";

    public const string ReportView = "REPORT_VIEW";
    public const string ReportExport = "REPORT_EXPORT";

    public const string UserView = "USER_VIEW";
    public const string UserCreate = "USER_CREATE";
    public const string UserEdit = "USER_EDIT";
    public const string UserRoleAssign = "USER_ROLE_ASSIGN";

    public const string BranchView = "BRANCH_VIEW";
    public const string BranchEdit = "BRANCH_EDIT";

    public const string SettingsView = "SETTINGS_VIEW";
    public const string SettingsEdit = "SETTINGS_EDIT";
}
