namespace ErpPersonelLeaveSystem.models
{
    // simülatör ve datagrid ekranına hesaplama sonuçlarını tek paket halinde taşımak için kullandığımız sınıf
    public class PayrollCalculationResult
    {
        public decimal BaseMonthlySalary { get; set; } //aylık brüt maaş
        public LeaveType LeaveType { get; set; } // izin türü
        public int LeaveDays { get; set; } // kullanılan izin gün sayısı

        //günlük maaş (brüt/30) otomatik hesaplanan alan
        public decimal DailyWage => Math.Round(BaseMonthlySalary / 30m, 2);

        //math round; eğer devirli bir sonuç gelirse dşye yuvarlama fonksyonu
        // 30'un yanında m olmasının nedeni: farklı iki tipte değişkende ( biri decimal diğeri int) hassas işlemler yaparken derleyici hata yapmasın diye birini diğerinin tipine çeviririz. burada da öyle yaptıks

        public decimal DeductionAmount { get; set; } //yapılan izin kesintisi tutarı
        public decimal FinalNetSalary { get; set; } // kesinti sonrası ödenecek net maaş 
        public string FormulaApplied { get; set; } = string.Empty; // uygulanan formülün açıklaması


    }

}
  