namespace ErpPersonelLeaveSystem.models;

public enum LeaveStatus
{
    Pending = 0,   // İzin Talebi Beklemede ⏳
    Approved = 1,  // İzin Onaylandı 🟢
    Rejected = 2   // İzin Reddedildi 🔴
}
