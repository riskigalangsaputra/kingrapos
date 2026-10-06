namespace KingraPOS.Application.Security;

public sealed class LicenseRestrictedException : Exception
{
    public LicenseRestrictedException(string message)
        : base(message)
    {
    }

    public static LicenseRestrictedException ForWrite() => new(
        "Masa berlaku lisensi sudah habis, sehingga aplikasi berjalan dalam mode terbatas. " +
        "Aktivasi lisensi diperlukan untuk menambah atau mengubah data.");
}
