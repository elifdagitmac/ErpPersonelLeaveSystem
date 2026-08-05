/*biz bu programı farklı bir yerde kullnmayacağımız için teorik olarak ILeaveCalculationService 
kullanmamıza gerek yok ama eğer farklı bir şubede geliştirmek isteseydik mesela o zman kullanırdık
ve LeaveCalculationService'in nasıl tanımlanması, nasıl kullanılması gerektiğini paylaşmış lurduk
yani bu sözleşme olası bir karışıklığın önüne geçmiş olurdu */



using ErpPersonelLeaveSystem.models;

namespace ErpPersonelLeaveSystem.Services
{
    // ILeaveCalculationService sözleşmesini uygulayan gerçek hesaplama servisimiz 
    public class LeaveCalculationService : ILeaveCalculationService
    {
        public PayrollCalculationResult CalculatePayroll(decimal monthlySalary, LeaveType leaveType, int leaveDays) // burada CalculatePayroll fonksiyonunu doldurduk, hangi durum için nasıl çalışacağını belirledik
        {
            //günlük maaşı hesaplama
            decimal dailyWage = Math.Round(monthlySalary / 30m, 2);
            decimal deduction = 0m;
            string formula = "";

            //izin türüne göre kesinti kurallarını çalıştır
            switch (leaveType) //İZİN TÜRÜNE GÖRE DEĞİŞİR
            {
                case LeaveType.YillikIzin:
                    deduction = 0m; //kesinti=0TL anlamına gelir
                    formula = "Yıllık İzin: Kesinti Yapılmaz.";
                    break;

                case LeaveType.UcretsizIzin:
                    deduction = Math.Round(dailyWage * leaveDays, 2); //kesinti hesaplandı (günlük maaş * izinli günler)
                    formula = $" Ücretsiz İzin: Günlük Yevmiye ({dailyWage:C2}) x {leaveDays} gün = Kesinti ({deduction})";
                    break;

                case LeaveType.SaglikIzni:
                case LeaveType.MazaretIzni:
                    deduction = 0m; //kesinti = 0 TL anlamına gelir 
                    formula = "Mazaret/Sağlık İzni: Şirket politikası gereği kesinti uygulanmaz.";
                    break;

                default:
                    deduction = 0m; //kesinti = 0tl anlamına gelir 
                    formula = "Standart Hesaplama Yapıldı.";
                    break;
                //beklenmeyen, tanımlanmamış veya hatalı bir durum geldiğinde sistemin patlamasını/çökmesini engellemek için, varsayılan durum çalıştırmak gerekir

            }

            // net maaşı hesapla (maaşın eksiye düşmesini engelle)
            decimal finalNetSalary = Math.Max(0, monthlySalary - deduction);

            //tüm hesaplama sonuçlarını paketleyip geriye dön
            return new PayrollCalculationResult
            {
                BaseMonthlySalary = monthlySalary,
                LeaveType = leaveType,
                LeaveDays = leaveDays,
                DeductionAmount = deduction,
                FinalNetSalary = finalNetSalary,
                FormulaApplied = formula
                //fonksiyondan elde edilen değişkenler tablodaki sütunların içerisine yerleştirilir.
            };



        }

    }

}
