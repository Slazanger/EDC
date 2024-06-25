using System;
using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace EveDataCollator.EDCEF
{
    public static class YamlParser
    {
        public static T ParseYamlValue<T>(YamlMappingNode root, string key, Func<string, T> parseFunction)
        {
            if (root.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalarNode)
            {
                return parseFunction(scalarNode.Value);
            }
            else
            {
                //Console.WriteLine($"Warning: Key '{key}' not found or not a scalar node. Using default value '{default(T)}'.");
                return default;
            }
        }

        public static bool ParseBool(string value) => bool.Parse(value);
        public static float ParseFloat(string value) => float.Parse(value, CultureInfo.InvariantCulture);
        public static decimal ParseDecimal(string value) => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        public static int ParseInt(string value) => int.Parse(value);
    }
}