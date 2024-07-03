using System;
using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace EveDataCollator.EDCEF
{
    public static class YamlParser
    {
        public static T ParseYamlValue<T>(YamlNode root, string key, Func<string, T> parseFunction)
        {
            if (root is YamlMappingNode mappingNode)
            {
                if (mappingNode.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalarNode)
                {
                    try
                    {
                        return parseFunction(scalarNode.Value);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing value for key '{key}': {ex.Message}");
                        return default;
                    }
                }
                else
                {
                    // Key not found or not a scalar node
                    // Console.WriteLine($"Warning: Key '{key}' not found or not a scalar node. Using default value '{default(T)}'.");
                    return default;
                }
            }
            else if (root is YamlScalarNode scalarNode && scalarNode.Value == key)
            {
                // Handle the case where root is the scalar node itself
                try
                {
                    return parseFunction(scalarNode.Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing value for scalar node '{key}': {ex.Message}");
                    return default;
                }
            }
            else
            {
                // Handle other node types or unexpected cases
                // Console.WriteLine($"Warning: Unexpected node type for root. Using default value '{default(T)}'.");
                return default;
            }
        }

        public static string ParseString(string value) => value;
        public static bool ParseBool(string value) => bool.Parse(value);
        public static float ParseFloat(string value) => float.Parse(value, CultureInfo.InvariantCulture);
        public static decimal ParseDecimal(string value) => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        public static int ParseInt(string value) => int.Parse(value);
    }
}