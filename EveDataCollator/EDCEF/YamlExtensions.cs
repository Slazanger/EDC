using System.Globalization;
using EveDataCollator.Data;
using YamlDotNet.RepresentationModel;

namespace EveDataCollator.EDCEF
{

    public static class YamlExtensions
    {
        public static DecVector3 ToDecVector3(this YamlSequenceNode sequenceNode)
        {
            if (sequenceNode.Children.Count != 3)
            {
                throw new ArgumentException("The sequence node must contain exactly three elements.");
            }

            decimal x = decimal.Parse(sequenceNode.Children[0].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
            decimal y = decimal.Parse(sequenceNode.Children[1].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
            decimal z = decimal.Parse(sequenceNode.Children[2].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);

            return new DecVector3(x, y, z);
        }
    }
}