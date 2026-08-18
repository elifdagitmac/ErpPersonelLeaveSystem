using ErpPersonelLeaveSystem.models;

namespace ErpPersonelLeaveSystem.Services;

public interface ILeaveCalculationService
{
    PayrollCalculationResult CalculatePayroll(decimal monthlySalary, LeaveType leaveType, decimal leaveDays);
}