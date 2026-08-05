using ErpPersonelLeaveSystem.models;

namespace ErpPersonelLeaveSystem.Services;

public interface ILeaveCalculationService
{
    PayrollCalculationResult CalculatePayroll(decimal monthlySalary, LeaveType leaveType, int leaveDays);
}

//dışarıdan monthlysalary, leavetype, leavedays parametreleri alınır, calculatepayroll fonksiyonu hesaplamayı yapıp geriye net maaşı ve kesinti miktarını ( payrollcalculationresult ) geri döndürür.
// burada CalculatePayroll fonksiyonunu Interface içerisinden import ettik 