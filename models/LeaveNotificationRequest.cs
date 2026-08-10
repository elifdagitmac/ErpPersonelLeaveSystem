namespace ErpPersonelLeaveSystem.models
{
    public class LeaveNotificationRequest
    {
        public int LeaveRecordId { get; set; }
        public string MessageNote { get; set; } = string.Empty;
    }
}
