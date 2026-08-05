namespace ErpPersonelLeaveSystem.models
{
    //Metin olarak "Yıllık İzin" yazmak yerine her duruma bir sayısal değer verip, onları hafızada sayısal değerler olarak tutmak database de daha az yer kaplar.

    public enum LeaveType //listes tanımlandığı için ; kullanılmadı
    {
        YillikIzin = 1, //ücretli yıllık izin - maaş kesintisi yok
        UcretsizIzin = 2, //ücretsiz izin - günlük maaş ile hesaplanarak kesinti yapılır
        SaglikIzni = 3, //sağlık/rapor izni kesintisiz
        MazaretIzni = 4 //kesintisiz
    }

}


