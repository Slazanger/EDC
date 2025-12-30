using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using EveDataCollator.EDCEF;
using EveDataCollator.EVE.Universe;
using Microsoft.EntityFrameworkCore.Storage;
using YamlDotNet.RepresentationModel;

namespace EveDataCollator
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            string latestJsonlUrl = @"https://developers.eveonline.com/static-data/tranquility/latest.jsonl";
            
            // Get the latest build number from the server
            int buildNumber = await GetLatestBuildNumberAsync(latestJsonlUrl);
            Console.WriteLine($"Latest SDE build number: {buildNumber}");

            // Check if we already have this build number locally
            string dataFolder = $"{System.AppContext.BaseDirectory}{buildNumber}";
            
            if (Directory.Exists(dataFolder))
            {
                Console.WriteLine($"Build {buildNumber} already exists locally at {dataFolder}");
                Console.WriteLine($"Skipping download and using existing data");
            }
            else
            {
                Console.WriteLine($"Build {buildNumber} not found locally. Downloading...");
                await DownloadSDEJsonlAsync(buildNumber, dataFolder);
                Console.WriteLine($"Download and extraction complete. Data available at {dataFolder}");
            }

            LoadNameDictionary(dataFolder);
            Inventory.Parse(dataFolder);
        }


        /// <summary>
        /// Get the latest build number from the SDE latest.jsonl file
        /// </summary>
        /// <param name="latestJsonlUrl">URL of the latest.jsonl file</param>
        /// <returns>The build number as an integer</returns>
        private static async Task<int> GetLatestBuildNumberAsync(string latestJsonlUrl)
        {
            using var client = new HttpClient();
            string content = await client.GetStringAsync(latestJsonlUrl);
            
            // Parse the JSONL content (single line JSON)
            using JsonDocument doc = JsonDocument.Parse(content);
            JsonElement root = doc.RootElement;
            
            if (root.TryGetProperty("buildNumber", out JsonElement buildNumberElement))
            {
                return buildNumberElement.GetInt32();
            }
            
            throw new InvalidOperationException("Could not find buildNumber in latest.jsonl response");
        }

        /// <summary>
        /// Download and extract the JSONL variant of the SDE for the specified build number
        /// </summary>
        /// <param name="buildNumber">The build number to download</param>
        /// <param name="dataFolder">The destination folder for the extracted data</param>
        private static async Task DownloadSDEJsonlAsync(int buildNumber, string dataFolder)
        {
            string sdeUrl = $"https://developers.eveonline.com/static-data/tranquility/eve-online-static-data-{buildNumber}-jsonl.zip";
            string tempFolder = $"{System.AppContext.BaseDirectory}temp";
            string zipFile = Path.Combine(tempFolder, $"sde-{buildNumber}-jsonl.zip");

            // Clear out any temp folder
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
            Directory.CreateDirectory(tempFolder);

            // Download the SDE zip file
            Console.WriteLine($"Downloading SDE from: {sdeUrl}");
            await DownloadFile(sdeUrl, zipFile);
            Console.WriteLine($"Downloaded to: {zipFile}");

            // Extract the zip file
            Console.WriteLine("Extracting SDE zip file...");
            using (ZipArchive sdeZipArchive = ZipFile.OpenRead(zipFile))
            {
                string extractPath = Path.Combine(tempFolder, "extracted");
                Directory.CreateDirectory(extractPath);

                foreach (ZipArchiveEntry entry in sdeZipArchive.Entries)
                {
                    string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
                    string? destinationDirectory = Path.GetDirectoryName(destinationPath);
                    
                    if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                    }

                    // Only extract files (not directories)
                    if (!string.IsNullOrEmpty(entry.Name))
                    {
                        entry.ExtractToFile(destinationPath, true);
                    }
                }

                // Move extracted data to the final build number folder
                if (Directory.Exists(dataFolder))
                {
                    Directory.Delete(dataFolder, true);
                }
                Directory.Move(extractPath, dataFolder);
            }

            // Clean up temporary files
            if (File.Exists(zipFile))
            {
                File.Delete(zipFile);
            }
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
        }

        /// <summary>
        /// download file helper
        /// </summary>
        /// <param name="fileUrl">remote url</param>
        /// <param name="localFile">local file</param>
        /// <returns></returns>
        private static async Task DownloadFile(string fileUrl, string localFile)
        {
            using var httpClient = new HttpClient();
            using var responseStream = await httpClient.GetStreamAsync(fileUrl);
            using var fileStream = new FileStream(localFile, FileMode.OpenOrCreate);
            await responseStream.CopyToAsync(fileStream);
        }

        /// <summary>
        /// parse and create the id to name dictionary which is used in most subsequent parse operations
        /// </summary>
        /// <param name="rootFolder">Folder with the extracted SDE in</param>
        private static void LoadNameDictionary(string rootFolder)
        {
            Globals.NameIDDictionary.Clear();

            string dictionaryFile = $"{rootFolder}\\types.jsonl";

            if (!File.Exists(dictionaryFile))
            {
                Console.WriteLine($"Warning: types.jsonl not found at {dictionaryFile}");
                return;
            }

            using var sr = new StreamReader(dictionaryFile);
            string? jsonObject;
            int objectNumber = 0;

            while ((jsonObject = JsonlHelper.ReadCompleteJsonObject(sr)) != null)
            {
                objectNumber++;

                if (string.IsNullOrWhiteSpace(jsonObject))
                {
                    continue;
                }

                try
                {
                    using JsonDocument doc = JsonDocument.Parse(jsonObject);
                    JsonElement root = doc.RootElement;

                    // Get the item ID from _key field
                    int itemID = 0;
                    if (root.TryGetProperty("_key", out JsonElement keyElement))
                    {
                        // _key can be either a number or a string
                        if (keyElement.ValueKind == JsonValueKind.Number)
                        {
                            itemID = keyElement.GetInt32();
                        }
                        else if (keyElement.ValueKind == JsonValueKind.String)
                        {
                            if (!int.TryParse(keyElement.GetString(), out itemID))
                            {
                                continue; // Skip invalid item IDs
                            }
                        }
                        else
                        {
                            continue; // Skip if _key is not a number or string
                        }
                    }
                    else if (root.TryGetProperty("itemID", out JsonElement itemIdElement))
                    {
                        itemID = itemIdElement.GetInt32();
                    }
                    else
                    {
                        continue; // Skip if no ID field found
                    }

                    // Get the item name
                    string itemName = string.Empty;
                    if (root.TryGetProperty("itemName", out JsonElement itemNameElement))
                    {
                        itemName = itemNameElement.GetString() ?? string.Empty;
                    }
                    else if (root.TryGetProperty("name", out JsonElement nameElement))
                    {
                        // Handle multilingual name object
                        if (nameElement.ValueKind == JsonValueKind.Object)
                        {
                            if (nameElement.TryGetProperty("en", out JsonElement nameEnElement))
                            {
                                itemName = nameEnElement.GetString() ?? string.Empty;
                            }
                        }
                        else
                        {
                            itemName = nameElement.GetString() ?? string.Empty;
                        }
                    }

                    if (!string.IsNullOrEmpty(itemName))
                    {
                        Globals.NameIDDictionary[itemID] = itemName;
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error parsing JSON object {objectNumber}: {ex.Message}");
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error on object {objectNumber}: {ex.Message}");
                    continue;
                }
            }
        }



    }
}