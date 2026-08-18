using ErpPersonelLeaveSystem.models;

namespace ErpPersonelLeaveSystem.Services;

public class LeaveCalculationService : ILeaveCalculationService
{
    public PayrollCalculationResult CalculatePayroll(decimal monthlySalary, LeaveType leaveType, decimal leaveDays)
    {
        decimal dailyWage = monthlySalary / 30m;
        decimal deductionAmount = 0m;
        string formulaApplied = "";

        switch (leaveType)
        {
            case LeaveType.YillikIzin:
            case LeaveType.SaglikIzni:
            case LeaveType.MazaretIzni:
                deductionAmount = 0m;
                formulaApplied = $"{leaveType}: Ücretli İzin (Maaş Kesintisi Yapılmadı).";
                break;

            case LeaveType.UcretsizIzin:
                deductionAmount = dailyWage * leaveDays;
                formulaApplied = $"Ücretsiz İzin: Günlük Yevmiye (₺{dailyWage:N2}) x {leaveDays} gün = Kesinti (₺{deductionAmount:N2})";
                break;
        }

        decimal finalNetSalary = monthlySalary - deductionAmount;

        return new PayrollCalculationResult
        {
            BaseMonthlySalary = monthlySalary,
            LeaveType = leaveType,
            DeductionAmount = deductionAmount,
            FinalNetSalary = finalNetSalary,
            FormulaApplied = formulaApplied
        };
    }
}