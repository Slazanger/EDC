using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
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
        public static Dictionary<int, EveDataCollator.Eve.Inventory.Category> Categories { get; set; }

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




            //ParseBluePrints(rootFolder);
            ParseCategories(rootFolder);
            ParseGroups(rootFolder);
            ParseMarketGroups(rootFolder);

            // must happen after groups and categories are parsed
            ParseTypesJsonl(rootFolder);


            CalculatePackagedVolumes().Wait(); // Wait for the async method to complete


            ExportItemDataBase($"{rootFolder}\\PublishedItemDatabase.csv");
        }


        private static async Task CalculatePackagedVolumes()
        {
            using var httpClient = new HttpClient();
            string esiBaseUrl = "https://esi.evetech.net/latest/universe/types";

            foreach(var type in Types.Values)
            {
                try
                {
                    // Construct the URL for the typeID
                    string url = $"{esiBaseUrl}/{type.Id}/?datasource=tranquility";

                    // Make the HTTP GET request
                    var response = await httpClient.GetAsync(url);

                    if(response.IsSuccessStatusCode)
                    {
                        // Parse the JSON response
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var jsonDocument = JsonDocument.Parse(jsonResponse);

                        // Extract the packaged_volume field
                        if(jsonDocument.RootElement.TryGetProperty("packaged_volume", out var packagedVolumeElement))
                        {
                            type.PackagedVolume = packagedVolumeElement.GetDouble();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to fetch data for typeID {type.Id}: {response.StatusCode}");
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Error fetching packaged_volume for typeID {type.Id}: {ex.Message}");
                }
            }
        }


        private static void ParseBluePrints(string rootFolder)
        {
            string blueprintsResourceFile = $"{rootFolder}\\fsd\\blueprints.yaml";

            using var sr = new StreamReader(blueprintsResourceFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

            foreach(var e in root.Children)
            {
                YamlScalarNode blueprintIdNode = (YamlScalarNode)e.Key;

                if(blueprintIdNode.Value == null || !int.TryParse(blueprintIdNode.Value, out int blueprintID))
                {
                    continue; // Skip invalid or missing blueprint IDs
                }

                YamlMappingNode blueprintNode = (YamlMappingNode)e.Value;

                // Check if the blueprint has a manufacturing section
                if(blueprintNode.Children.ContainsKey("activities"))
                {
                    var activitiesNode = (YamlMappingNode)blueprintNode["activities"];

                    if(activitiesNode.Children.ContainsKey("manufacturing"))
                    {
                        var manufacturingNode = (YamlMappingNode)activitiesNode["manufacturing"];

                        if(manufacturingNode.Children.ContainsKey("products"))
                        {
                            var productsNode = (YamlSequenceNode)manufacturingNode["products"];

                            foreach(var product in productsNode.Children)
                            {
                                var productNode = (YamlMappingNode)product;

                                if(productNode.Children.ContainsKey("typeID"))
                                {
                                    var typeIdNode = (YamlScalarNode)productNode["typeID"];

                                    if(typeIdNode.Value != null && int.TryParse(typeIdNode.Value, out int typeID))
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
            string categoriesResourceFile = $"{rootFolder}\\categories.jsonl";

            if(!File.Exists(categoriesResourceFile))
            {
                Console.WriteLine($"Warning: categories.jsonl not found at {categoriesResourceFile}");
                return;
            }

            using var sr = new StreamReader(categoriesResourceFile);
            string? jsonObject;
            int objectNumber = 0;

            while((jsonObject = JsonlHelper.ReadCompleteJsonObject(sr)) != null)
            {
                objectNumber++;

                if(string.IsNullOrWhiteSpace(jsonObject))
                {
                    continue;
                }

                try
                {
                    using JsonDocument doc = JsonDocument.Parse(jsonObject);
                    JsonElement root = doc.RootElement;

                    // Get the category ID from _key field
                    if(!root.TryGetProperty("_key", out JsonElement keyElement))
                    {
                        continue; // Skip if no _key field
                    }

                    int categoryID = 0;
                    // _key can be either a number or a string
                    if(keyElement.ValueKind == JsonValueKind.Number)
                    {
                        categoryID = keyElement.GetInt32();
                    }
                    else if(keyElement.ValueKind == JsonValueKind.String)
                    {
                        if(!int.TryParse(keyElement.GetString(), out categoryID))
                        {
                            continue; // Skip invalid category IDs
                        }
                    }
                    else
                    {
                        continue; // Skip if _key is not a number or string
                    }

                    // Extract name.en
                    string name = string.Empty;
                    if(root.TryGetProperty("name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.Object)
                    {
                        if(nameElement.TryGetProperty("en", out JsonElement nameEnElement))
                        {
                            name = nameEnElement.GetString() ?? string.Empty;
                        }
                    }

                    // Extract published
                    bool published = false;
                    if(root.TryGetProperty("published", out JsonElement publishedElement))
                    {
                        published = publishedElement.GetBoolean();
                    }

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
                catch(JsonException ex)
                {
                    Console.WriteLine($"Error parsing JSON object {objectNumber}: {ex.Message}");
                    continue;
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Unexpected error on object {objectNumber}: {ex.Message}");
                    continue;
                }
            }
        }


        private static void ParseMarketGroups(string rootFolder)
        {
            string marketGroupsResourceFile = $"{rootFolder}\\marketGroups.jsonl";

            if(!File.Exists(marketGroupsResourceFile))
            {
                Console.WriteLine($"Warning: marketGroups.jsonl not found at {marketGroupsResourceFile}");
                return;
            }

            using var sr = new StreamReader(marketGroupsResourceFile);
            string? jsonObject;
            int objectNumber = 0;

            while((jsonObject = JsonlHelper.ReadCompleteJsonObject(sr)) != null)
            {
                objectNumber++;

                if(string.IsNullOrWhiteSpace(jsonObject))
                {
                    continue;
                }

                try
                {
                    using JsonDocument doc = JsonDocument.Parse(jsonObject);
                    JsonElement root = doc.RootElement;

                    // Get the market group ID from _key field
                    if(!root.TryGetProperty("_key", out JsonElement keyElement))
                    {
                        continue; // Skip if no _key field
                    }

                    int marketGroupID = 0;
                    // _key can be either a number or a string
                    if(keyElement.ValueKind == JsonValueKind.Number)
                    {
                        marketGroupID = keyElement.GetInt32();
                    }
                    else if(keyElement.ValueKind == JsonValueKind.String)
                    {
                        if(!int.TryParse(keyElement.GetString(), out marketGroupID))
                        {
                            continue; // Skip invalid market group IDs
                        }
                    }
                    else
                    {
                        continue; // Skip if _key is not a number or string
                    }

                    // Extract name.en (from nameID object or name object)
                    string name = string.Empty;
                    if(root.TryGetProperty("nameID", out JsonElement nameIdElement) && nameIdElement.ValueKind == JsonValueKind.Object)
                    {
                        if(nameIdElement.TryGetProperty("en", out JsonElement nameEnElement))
                        {
                            name = nameEnElement.GetString() ?? string.Empty;
                        }
                    }
                    else if(root.TryGetProperty("name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.Object)
                    {
                        if(nameElement.TryGetProperty("en", out JsonElement nameEnElement2))
                        {
                            name = nameEnElement2.GetString() ?? string.Empty;
                        }
                    }

                    // Extract description.en (from descriptionID object or description object)
                    string description = string.Empty;
                    if(root.TryGetProperty("descriptionID", out JsonElement descIdElement) && descIdElement.ValueKind == JsonValueKind.Object)
                    {
                        if(descIdElement.TryGetProperty("en", out JsonElement descEnElement))
                        {
                            description = descEnElement.GetString() ?? string.Empty;
                        }
                    }
                    else if(root.TryGetProperty("description", out JsonElement descElement) && descElement.ValueKind == JsonValueKind.Object)
                    {
                        if(descElement.TryGetProperty("en", out JsonElement descEnElement2))
                        {
                            description = descEnElement2.GetString() ?? string.Empty;
                        }
                    }

                    // Extract other fields
                    int parentGroupID = -1;
                    if(root.TryGetProperty("parentGroupID", out JsonElement parentGroupIdElement))
                    {
                        parentGroupID = parentGroupIdElement.GetInt32();
                    }

                    bool published = false;
                    if(root.TryGetProperty("published", out JsonElement publishedElement))
                    {
                        published = publishedElement.GetBoolean();
                    }

                    int iconID = -1;
                    if(root.TryGetProperty("iconID", out JsonElement iconIdElement))
                    {
                        iconID = iconIdElement.GetInt32();
                    }

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
                catch(JsonException ex)
                {
                    Console.WriteLine($"Error parsing JSON object {objectNumber}: {ex.Message}");
                    continue;
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Unexpected error on object {objectNumber}: {ex.Message}");
                    continue;
                }
            }
        }


        /// <summary>
        /// Parse the groups
        /// </summary>
        /// <param name="rootFolder">The extracted root folder of the SDE</param>
        private static void ParseGroups(string rootFolder)
        {
            string groupsResourceFile = $"{rootFolder}\\groups.jsonl";

            if(!File.Exists(groupsResourceFile))
            {
                Console.WriteLine($"Warning: groups.jsonl not found at {groupsResourceFile}");
                return;
            }

            using var sr = new StreamReader(groupsResourceFile);
            string? jsonObject;
            int objectNumber = 0;

            while((jsonObject = JsonlHelper.ReadCompleteJsonObject(sr)) != null)
            {
                objectNumber++;

                if(string.IsNullOrWhiteSpace(jsonObject))
                {
                    continue;
                }

                try
                {
                    using JsonDocument doc = JsonDocument.Parse(jsonObject);
                    JsonElement root = doc.RootElement;

                    // Get the group ID from _key field
                    if(!root.TryGetProperty("_key", out JsonElement keyElement))
                    {
                        continue; // Skip if no _key field
                    }

                    int groupID = 0;
                    // _key can be either a number or a string
                    if(keyElement.ValueKind == JsonValueKind.Number)
                    {
                        groupID = keyElement.GetInt32();
                    }
                    else if(keyElement.ValueKind == JsonValueKind.String)
                    {
                        if(!int.TryParse(keyElement.GetString(), out groupID))
                        {
                            continue; // Skip invalid group IDs
                        }
                    }
                    else
                    {
                        continue; // Skip if _key is not a number or string
                    }

                    // Extract name.en
                    string name = string.Empty;
                    if(root.TryGetProperty("name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.Object)
                    {
                        if(nameElement.TryGetProperty("en", out JsonElement nameEnElement))
                        {
                            name = nameEnElement.GetString() ?? string.Empty;
                        }
                    }

                    // Extract other fields
                    int categoryID = -1;
                    if(root.TryGetProperty("categoryID", out JsonElement categoryIdElement))
                    {
                        categoryID = categoryIdElement.GetInt32();
                    }

                    bool published = false;
                    if(root.TryGetProperty("published", out JsonElement publishedElement))
                    {
                        published = publishedElement.GetBoolean();
                    }

                    bool anchorable = false;
                    if(root.TryGetProperty("anchorable", out JsonElement anchorableElement))
                    {
                        anchorable = anchorableElement.GetBoolean();
                    }

                    bool anchored = false;
                    if(root.TryGetProperty("anchored", out JsonElement anchoredElement))
                    {
                        anchored = anchoredElement.GetBoolean();
                    }

                    bool fittableNonSingleton = false;
                    if(root.TryGetProperty("fittableNonSingleton", out JsonElement fittableElement))
                    {
                        fittableNonSingleton = fittableElement.GetBoolean();
                    }

                    bool useBasePrice = false;
                    if(root.TryGetProperty("useBasePrice", out JsonElement useBasePriceElement))
                    {
                        useBasePrice = useBasePriceElement.GetBoolean();
                    }

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
                catch(JsonException ex)
                {
                    Console.WriteLine($"Error parsing JSON object {objectNumber}: {ex.Message}");
                    continue;
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Unexpected error on object {objectNumber}: {ex.Message}");
                    continue;
                }
            }
        }




        /// <summary>
        /// Parse the main items file types.jsonl looking for published items
        /// </summary>
        /// <param name="rootFolder">The extracted root folder of the SDE</param>
        private static void ParseTypesJsonl(string rootFolder)
        {
            string typesResourceFile = $"{rootFolder}\\types.jsonl";

            if(!File.Exists(typesResourceFile))
            {
                Console.WriteLine($"Warning: types.jsonl not found at {typesResourceFile}");
                return;
            }

            using var sr = new StreamReader(typesResourceFile);
            string? jsonObject;
            int objectNumber = 0;

            while((jsonObject = JsonlHelper.ReadCompleteJsonObject(sr)) != null)
            {
                objectNumber++;

                if(string.IsNullOrWhiteSpace(jsonObject))
                {
                    continue;
                }

                try
                {
                    using JsonDocument doc = JsonDocument.Parse(jsonObject);
                    JsonElement root = doc.RootElement;

                    // Get the type ID from _key field
                    if(!root.TryGetProperty("_key", out JsonElement keyElement))
                    {
                        continue; // Skip if no _key field
                    }

                    int typeID = 0;
                    // _key can be either a number or a string
                    if(keyElement.ValueKind == JsonValueKind.Number)
                    {
                        typeID = keyElement.GetInt32();
                    }
                    else if(keyElement.ValueKind == JsonValueKind.String)
                    {
                        if(!int.TryParse(keyElement.GetString(), out typeID))
                        {
                            continue; // Skip invalid type IDs
                        }
                    }
                    else
                    {
                        continue; // Skip if _key is not a number or string
                    }

                    // Check if published
                    bool published = false;
                    if(root.TryGetProperty("published", out JsonElement publishedElement))
                    {
                        published = publishedElement.GetBoolean();
                    }

                    // Only process published types
                    if(!published)
                    {
                        continue;
                    }

                    // Extract name.en
                    string name = string.Empty;
                    if(root.TryGetProperty("name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.Object)
                    {
                        if(nameElement.TryGetProperty("en", out JsonElement nameEnElement))
                        {
                            name = nameEnElement.GetString() ?? string.Empty;
                        }
                    }

                    // Extract description.en
                    string description = string.Empty;
                    if(root.TryGetProperty("description", out JsonElement descriptionElement) && descriptionElement.ValueKind == JsonValueKind.Object)
                    {
                        if(descriptionElement.TryGetProperty("en", out JsonElement descEnElement))
                        {
                            description = descEnElement.GetString() ?? string.Empty;
                        }
                    }

                    // Extract other fields
                    int groupID = 0;
                    if(root.TryGetProperty("groupID", out JsonElement groupIdElement))
                    {
                        groupID = groupIdElement.GetInt32();
                    }

                    int categoryID = 0;
                    if(groupID != 0 && Groups.TryGetValue(groupID, out var group))
                    {
                        categoryID = group.CategoryID;
                    }

                    int marketGroupID = 0;
                    if(root.TryGetProperty("marketGroupID", out JsonElement marketGroupIdElement))
                    {
                        marketGroupID = marketGroupIdElement.GetInt32();
                    }

                    int iconID = 0;
                    if(root.TryGetProperty("iconID", out JsonElement iconIdElement))
                    {
                        iconID = iconIdElement.GetInt32();
                    }

                    double basePrice = 0.0;
                    if(root.TryGetProperty("basePrice", out JsonElement basePriceElement))
                    {
                        basePrice = basePriceElement.GetDouble();
                    }

                    double volume = 0.0;
                    if(root.TryGetProperty("volume", out JsonElement volumeElement))
                    {
                        volume = volumeElement.GetDouble();
                    }

                    

                    // Create a new Type object and populate its properties
                    var type = new EveDataCollator.Eve.Inventory.Type
                    {
                        Id = typeID,
                        Name = name,
                        Description = description,
                        CategoryId = categoryID,
                        GroupId = groupID,
                        MarketGroupID = marketGroupID,
                        IconId = iconID,
                        BasePrice = basePrice,
                        Volume = volume,
                        Published = published
                    };

                    // Add the type to the dictionary
                    Types[typeID] = type;
                }
                catch(JsonException ex)
                {
                    Console.WriteLine($"Error parsing JSON object {objectNumber}: {ex.Message}");
                    continue;
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Unexpected error on object {objectNumber}: {ex.Message}");
                    continue;
                }
            }
        }


        /// <summary>
        /// Export all published items to a CSV file
        /// </summary>
        /// <param name="outputPath">Path where the CSV file should be created</param>
        public static void ExportItemDataBase(string outputPath)
        {
            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

            // Write CSV header
            writer.WriteLine("Id,Name,CategoryName,GroupName,BasePrice,Volume,PackagedVolume");

            // Iterate through all published types
            foreach(var type in Types.Values.Where(t => t.Published).OrderBy(t => t.Id))
            {
                // Look up category name from Categories dictionary
                string categoryName = string.Empty;
                if(Categories.TryGetValue(type.CategoryId, out var category))
                {
                    categoryName = category.Name;
                }

                // Look up group name from Groups dictionary
                string groupName = string.Empty;
                if(Groups.TryGetValue(type.GroupId, out var group))
                {
                    groupName = group.Name;
                }

                // Escape CSV values (handle commas, quotes, and newlines)
                string escapedName = EscapeCsvValue(type.Name);
                string escapedCategoryName = EscapeCsvValue(categoryName);
                string escapedGroupName = EscapeCsvValue(groupName);

                // Write CSV row
                writer.WriteLine($"{type.Id},{escapedName},{escapedCategoryName},{escapedGroupName},{type.BasePrice},{type.Volume},{type.PackagedVolume}");
            }

            Console.WriteLine($"Exported {Types.Values.Count(t => t.Published)} published items to {outputPath}");
        }

        /// <summary>
        /// Escape a value for CSV format (handles commas, quotes, and newlines)
        /// </summary>
        /// <param name="value">The value to escape</param>
        /// <returns>The escaped value</returns>
        private static string EscapeCsvValue(string value)
        {
            if(string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // If the value contains comma, quote, or newline, wrap it in quotes and escape internal quotes
            if(value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }

}
