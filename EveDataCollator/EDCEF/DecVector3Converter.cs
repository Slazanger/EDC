using EveDataCollator.Data;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EveDataCollator.EDCEF;

public class DecVector3Converter : ValueConverter<DecVector3, string>
{
    public DecVector3Converter() : base(
        v => $"{v.X},{v.Y},{v.Z}",
        v => new DecVector3(
            decimal.Parse(v.Split(',',StringSplitOptions.None)[0]),
            decimal.Parse(v.Split(',',StringSplitOptions.None)[1]),
            decimal.Parse(v.Split(',',StringSplitOptions.None)[2])))
    { }
}