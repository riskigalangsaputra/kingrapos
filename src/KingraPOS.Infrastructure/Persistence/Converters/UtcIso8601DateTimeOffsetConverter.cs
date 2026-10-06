using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KingraPOS.Infrastructure.Persistence.Converters;

public class UtcIso8601DateTimeOffsetConverter : ValueConverter<DateTimeOffset, string>
{
    private const string Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

    public UtcIso8601DateTimeOffsetConverter()
        : base(
            value => value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture),
            value => DateTimeOffset.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal))
    {
    }
}
