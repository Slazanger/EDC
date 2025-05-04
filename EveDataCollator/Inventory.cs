using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using EveDataCollator.EDCEF;
using EveDataCollator.Eve.Inventory;
using EveDataCollator.EVE.Universe;
using YamlDotNet.RepresentationModel;

namespace EveDataCollator
{
    public class Inventory
    {
        public static Dictionary<int, EveDataCollator.Eve.Inventory.Type> Types { get; set; }
        public static Dictionary<int, EveDataCollator.Eve.Inventory.Group> Groups { get; set; }
        public static Dictionary<int, EveDataCollator.Eve.Inventory.MarketGroup> MarketGroups { get; set; }
        public static Dictionary <int, EveDataCollator.Eve.Inventory.Category> Categories { get; set; }

        public static Dictionary<int, int> ItemToBluePrintID { get; set; }

        static Inventory()
        {
            Types = [];
            Groups = [];
            MarketGroups = [];
            Categories = [];
            ItemToBluePrintID = [];
        }

        /// <summary>
        /// Parse the items in game
        /// </summary>
        /// <param name="rootFolder">The extracted root folder of the SDE</param>
        public static void Parse(string rootFolder)
        {
            Types.Clear();
            Groups.Clear();
            MarketGroups.Clear();
            Categories.Clear();

            ParseBluePrints(rootFolder);
            ParseCategories(rootFolder);
            ParseGroups(rootFolder);
            ParseMarketGroups(rootFolder);
            ParseTypes(rootFolder);
        }


        private static void CalculatePackagedVolumes(string rootFolder)
        {
        
        }


        private static void ParseBluePrints(string rootFolder)
        {
            string blueprintsResourceFile = $"{rootFolder}\\fsd\\blueprints.yaml";

            using var sr = new StreamReader(blueprintsResourceFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

            foreach (var e in root.Children)
            {
                YamlScalarNode blueprintIdNode = (YamlScalarNode)e.Key;

                if (blueprintIdNode.Value == null || !int.TryParse(blueprintIdNode.Value, out int blueprintID))
                {
                    continue; // Skip invalid or missing blueprint IDs
                }

                YamlMappingNode blueprintNode = (YamlMappingNode)e.Value;

                // Check if the blueprint has a manufacturing section
                if (blueprintNode.Children.ContainsKey("activities"))
                {
                    var activitiesNode = (YamlMappingNode)blueprintNode["activities"];

                    if (activitiesNode.Children.ContainsKey("manufacturing"))
                    {
                        var manufacturingNode = (YamlMappingNode)activitiesNode["manufacturing"];

                        if (manufacturingNode.Children.ContainsKey("products"))
                        {
                            var productsNode = (YamlSequenceNode)manufacturingNode["products"];

                            foreach (var product in productsNode.Children)
                            {
                                var productNode = (YamlMappingNode)product;

                                if (productNode.Children.ContainsKey("typeID"))
                                {
                                    var typeIdNode = (YamlScalarNode)productNode["typeID"];

                                    if (typeIdNode.Value != null && int.TryParse(typeIdNode.Value, out int typeID))
                                    {
                                        // Map the product typeID to the blueprint ID
                                        ItemToBluePrintID[typeID] = blueprintID;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void ParseCategories(string rootFolder)
        {
            string categoriesResourceFile = $"{rootFolder}\\fsd\\categories.yaml";

            using var sr = new StreamReader(categoriesResourceFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

            foreach (var e in root.Children)
            {
                YamlScalarNode idNode = (YamlScalarNode)e.Key;

                if (idNode.Value == null || !int.TryParse(idNode.Value, out int categoryID))
                {
                    continue; // Skip invalid or missing category IDs
                }

                YamlMappingNode categoryNode = (YamlMappingNode)e.Value;

                // Parse the required fields
                string name = string.Empty;
                if (categoryNode.Children.ContainsKey("name"))
                {
                    var nameNode = (YamlMappingNode)categoryNode["name"];
                    if (nameNode.Children.ContainsKey("en"))
                    {
                        name = ((YamlScalarNode)nameNode["en"]).Value!;
                    }
                }

                bool published = categoryNode.Children.ContainsKey("published")
                    ? YamlParser.ParseYamlValue(categoryNode, "published", YamlParser.ParseBool)
                    : false;

                // Create a new Category object and populate its properties
                var category = new Category
                {
                    Id = categoryID,
                    Name = name,
                    Published = published
                };

                // Add the category to the dictionary
                Categories[categoryID] = category;
            }
        }


        private static void ParseMarketGroups(string rootFolder)
        {
            string marketGroupsResourceFile = $"{rootFolder}\\fsd\\marketGroups.yaml";

            using var sr = new StreamReader(marketGroupsResourceFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

            foreach (var e in root.Children)
            {
                YamlScalarNode idNode = (YamlScalarNode)e.Key;

                if (idNode.Value == null || !int.TryParse(idNode.Value, out int marketGroupID))
                {
                    continue; // Skip invalid or missing market group IDs
                }

                YamlMappingNode marketGroupNode = (YamlMappingNode)e.Value;

                // Parse the required fields
                string name = string.Empty;
                if (marketGroupNode.Children.ContainsKey("nameID"))
                {
                    var nameNode = (YamlMappingNode)marketGroupNode["nameID"];
                    if (nameNode.Children.ContainsKey("en"))
                    {
                        name = ((YamlScalarNode)nameNode["en"]).Value!;
                    }
                }

                string description = string.Empty;
                if (marketGroupNode.Children.ContainsKey("descriptionID"))
                {
                    var descriptionNode = (YamlMappingNode)marketGroupNode["descriptionID"];
                    if (descriptionNode.Children.ContainsKey("en"))
                    {
                        description = ((YamlScalarNode)descriptionNode["en"]).Value!;
                    }
                }

                int parentGroupID = marketGroupNode.Children.ContainsKey("parentGroupID")
                    ? YamlParser.ParseYamlValue(marketGroupNode, "parentGroupID", YamlParser.ParseInt)
                    : -1;

                bool published = marketGroupNode.Children.ContainsKey("published")
                    ? YamlParser.ParseYamlValue(marketGroupNode, "published", YamlParser.ParseBool)
                    : false;

                int iconID = marketGroupNode.Children.ContainsKey("iconID")
                    ? YamlParser.ParseYamlValue(marketGroupNode, "iconID", YamlParser.ParseInt)
                    : -1;

                // Create a new MarketGroup object and populate its properties
                MarketGroup marketGroup = new MarketGroup
                {
                    Id = marketGroupID,
                    Name = name,
                    Description = description,
                    ParentGroupID = parentGroupID,
                    Published = published,
                    IcondID = iconID
                };

                // Add the market group to the dictionary
                MarketGroups[marketGroupID] = marketGroup;
            }
        }


        /// <summary>
        /// Parse the groups
        /// </summary>
        /// <param name="rootFolder">The extracted root folder of the SDE</param>
        private static void ParseGroups(string rootFolder)
        {
            string groupsResourceFile = $"{rootFolder}\\fsd\\groups.yaml";

            using var sr = new StreamReader(groupsResourceFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

            foreach (var e in root.Children)
            {
                YamlScalarNode idNode = (YamlScalarNode)e.Key;

                if (idNode.Value == null || !int.TryParse(idNode.Value, out int groupID))
                {
                    continue; // Skip invalid or missing group IDs
                }

                YamlMappingNode groupNode = (YamlMappingNode)e.Value;

                // Parse the required fields
                string name = string.Empty;
                if (groupNode.Children.ContainsKey("name"))
                {
                    var nameNode = (YamlMappingNode)groupNode["name"];
                    if (nameNode.Children.ContainsKey("en"))
                    {
                        name = ((YamlScalarNode)nameNode["en"]).Value!;
                    }
                }

                int categoryID = groupNode.Children.ContainsKey("categoryID")
                    ? YamlParser.ParseYamlValue(groupNode, "categoryID", YamlParser.ParseInt)
                    : -1;

                bool published = groupNode.Children.ContainsKey("published")
                    ? YamlParser.ParseYamlValue(groupNode, "published", YamlParser.ParseBool)
                    : false;

                bool anchorable = groupNode.Children.ContainsKey("anchorable")
                    ? YamlParser.ParseYamlValue(groupNode, "anchorable", YamlParser.ParseBool)
                    : false;

                bool anchored = groupNode.Children.ContainsKey("anchored")
                    ? YamlParser.ParseYamlValue(groupNode, "anchored", YamlParser.ParseBool)
                    : false;

                bool fittableNonSingleton = groupNode.Children.ContainsKey("fittableNonSingleton")
                    ? YamlParser.ParseYamlValue(groupNode, "fittableNonSingleton", YamlParser.ParseBool)
                    : false;

                bool useBasePrice = groupNode.Children.ContainsKey("useBasePrice")
                    ? YamlParser.ParseYamlValue(groupNode, "useBasePrice", YamlParser.ParseBool)
                    : false;

                // Create a new Group object and populate its properties
                var group = new Group
                {
                    Id = groupID,
                    Name = name,
                    CategoryID = categoryID,
                    Published = published,
                    Anchorable = anchorable,
                    Anchored = anchored,
                    FittableNonSingleton = fittableNonSingleton,
                    UseBasePrice = useBasePrice
                };

                // Add the group to the dictionary
                Groups[groupID] = group;
            }
        }



        /// <summary>
        /// Parse the main items file types.yaml looking for published items
        /// </summary>
        /// <param name="rootFolder">The extracted root folder of the SDE</param>
        private static void ParseTypes(string rootFolder)
        {
            string typesResourceFile = $"{rootFolder}\\fsd\\types.yaml";

            using var sr = new StreamReader(typesResourceFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

            foreach (var e in root.Children)
            {
                YamlScalarNode idNode = (YamlScalarNode)e.Key;

                if (idNode.Value == null || !int.TryParse(idNode.Value, out int typeID))
                {
                    continue; // Skip invalid or missing type IDs
                }

                YamlMappingNode typeNode = (YamlMappingNode)e.Value;

                if (typeNode.Children.ContainsKey("published"))
                {
                    bool published = YamlParser.ParseYamlValue(typeNode, "published", YamlParser.ParseBool);

                    if (published)
                    {
                        // Parse the required fields
                        string name = string.Empty;
                        if (typeNode.Children.ContainsKey("name"))
                        {
                            var nameNode = (YamlMappingNode)typeNode["name"];
                            if (nameNode.Children.ContainsKey("en"))
                            {
                                name = ((YamlScalarNode)nameNode["en"]).Value!;
                            }
                        }

                        string description = string.Empty;
                        if (typeNode.Children.ContainsKey("description"))
                        {
                            var descriptionNode = (YamlMappingNode)typeNode["description"];
                            if (descriptionNode.Children.ContainsKey("en"))
                            {
                                description = ((YamlScalarNode)descriptionNode["en"]).Value!;
                            }
                        }

                        int groupID = YamlParser.ParseYamlValue(typeNode, "groupID", YamlParser.ParseInt);
                        int iconID = YamlParser.ParseYamlValue(typeNode, "iconID", YamlParser.ParseInt);
                        int marketGroupID = YamlParser.ParseYamlValue(typeNode, "marketGroupID", YamlParser.ParseInt);
                        float basePrice = YamlParser.ParseYamlValue(typeNode, "basePrice", YamlParser.ParseFloat);
                        float volume = YamlParser.ParseYamlValue(typeNode, "volume", YamlParser.ParseFloat);

                        // Create a new Type object and populate its properties
                        var type = new EveDataCollator.Eve.Inventory.Type
                        {
                            Id = typeID,
                            Name = name,
                            Description = description,
                            GroupId = groupID,
                            IconId = iconID,
                            MarketGroupID = marketGroupID,
                            BasePrice = basePrice,
                            Volume = volume,
                            Published = published
                        };

                        // Add the type to the dictionary
                        Types[typeID] = type;
                    }
                }
            }
        }
    }
}
