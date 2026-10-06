using KingraPOS.Domain.Enums;

namespace KingraPOS.Application.Stock;

public enum RejectResolution
{
    ReturnedToSupplier = 0,
    WrittenOff = 1
}

public static class RejectResolutionMap
{
    public static RejectStatus ToStatus(RejectResolution resolution) => resolution switch
    {
        RejectResolution.ReturnedToSupplier => RejectStatus.RETURNED_TO_SUPPLIER,
        _ => RejectStatus.WRITTEN_OFF
    };
}
