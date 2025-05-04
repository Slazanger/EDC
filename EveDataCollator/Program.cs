using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
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
            string checksumUrl = @"https://eve-static-data-export.s3-eu-west-1.amazonaws.com/tranquility/checksum";
            string SDEUrl = @"https://eve-static-data-export.s3-eu-west-1.amazonaws.com/tranquility/sde.zip";
            string localCheckSumFile = $"{System.AppContext.BaseDirectory}previous-checksum";

            string serverSDECheckSum = await GetSdeCheckSumFromServer(checksumUrl);
            string localSDECheckSum = "";

            string tempFolder = $"{System.AppContext.BaseDirectory}temp";
            string dataFolder = tempFolder;

            if (File.Exists(localCheckSumFile))
            {
                localSDECheckSum = File.ReadAllText(localCheckSumFile);
                dataFolder = $"{System.AppContext.BaseDirectory}{localSDECheckSum}";
            }

            string SDELocal = $"{dataFolder}\\sde.zip";
            bool downloadSDE = false;

            if (localSDECheckSum != serverSDECheckSum)
            {
                downloadSDE = true;
            }

            // If either the data folder or the sde zip are missing, download it
            if (!Directory.Exists(dataFolder))
            {
                downloadSDE = true;
                Directory.CreateDirectory(dataFolder);
            }
            else
            {
                if (!File.Exists(SDELocal))
                {
                    downloadSDE = true;
                }
            }

            if (downloadSDE)
            {
                // Clear out any temp folder
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
                Directory.CreateDirectory(tempFolder);

                // download latest SDE
                await DownloadSDE(SDEUrl, SDELocal);

                // extract SDE zip while hashing the content
                using (ZipArchive sdeZipArchive = ZipFile.OpenRead(SDELocal))
                {
                    using (var md5 = MD5.Create())
                    {
                        // CCP uses a combined hash of every file in the zip as the checksum
                        foreach (ZipArchiveEntry entry in sdeZipArchive.Entries)
                        {
                            string destinationPath = Path.GetFullPath(Path.Combine(tempFolder, entry.FullName));
                            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
                            if (!Directory.Exists(destinationDirectory))
                            {
                                Directory.CreateDirectory(destinationDirectory!);
                            }
                            entry.ExtractToFile(destinationPath, true);

                            // add the file to the hash
                            using (var stream = File.OpenRead(destinationPath))
                            {
                                byte[] buffer = new byte[4096];
                                int bytesRead;
                                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                                }
                            }
                        }

                        // finalize the hash and write it to file
                        md5.TransformFinalBlock(new byte[0], 0, 0);
                        string hashString = BitConverter.ToString(md5.Hash!).Replace("-", "").ToLowerInvariant();
                        File.WriteAllText(localCheckSumFile, hashString);

                        // set the correct folder
                        dataFolder = $"{System.AppContext.BaseDirectory}{hashString}";
                    }
                }

                // if we downloaded data but there is a corresponding folder, delete it
                if (Directory.Exists(dataFolder))
                {
                    Directory.Delete(dataFolder, true);
                }

                // then move the new data to the data folder
                Directory.Move(tempFolder, dataFolder);
            }
            else
            {
                Console.WriteLine($"Skipping download and using {dataFolder}");
            }

            // load the string database
            LoadNameDictionary(dataFolder);

            // parse the inventory files
            Inventory.Parse(dataFolder);


            // collate all the universe files
            Universe.Parse(dataFolder);

            Console.WriteLine($"Parsed {Universe.Regions.Count} Regions");

            ExportUniverseToEfDb(Universe.Regions.Values.ToList());
        }

        /// <summary>
        /// Get the current Checksum for the latest released SDE
        /// </summary>
        /// <param name="checksumUrl">URL of the published SDE Checksum file</param>
        /// <returns></returns>
        private static async Task<string> GetSdeCheckSumFromServer(string checksumUrl)
        {
            // the checksum file contains a list of the contents
            // however the sde.zip is not the MD5 hash of the sde file
            // but is a hash of the contents incase the zip file re-orders
            // and they need re-publish the same file :|

            // so for now just assume the 2 match until this gets expanded

            string checksum = "Unknown";
            using (var client = new HttpClient())
            {
                string content = await client.GetStringAsync(checksumUrl);

                string[] lines = content.Split('\n');
                foreach (string line in lines)
                {
                    if (line.Contains("sde.zip"))
                    {
                        checksum = line.Split(" ")[0];
                        break;
                    }
                }
            }
            return checksum;
        }

        /// <summary>
        /// Download the latest SDE from CCP
        /// </summary>
        /// <param name="SDEUrl">SDE URL</param>
        /// <param name="localFile">Local File</param>
        private static async Task DownloadSDE(string SDEUrl, string localFile)
        {
            Console.WriteLine($"Downloading Latest SDE : {SDEUrl}");
            await DownloadFile(SDEUrl, localFile);
            Console.WriteLine($"Downloaded to {localFile}");
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

            string dictionaryFile = $"{rootFolder}\\bsd\\invNames.yaml";

            using var sr = new StreamReader(dictionaryFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlSequenceNode)yamlStream.Documents[0].RootNode;

            foreach (var e in root.Children)
            {
                YamlScalarNode itemIDNode = (YamlScalarNode)e["itemID"];
                if(itemIDNode.Value == null)
                {
                    Console.WriteLine($"Failed to parse item ID {e}");
                    continue;
                }
                int itemID = int.Parse(itemIDNode.Value);

                YamlScalarNode englishTextNode = (YamlScalarNode)e["itemName"];

                if(englishTextNode.Value == null)
                {
                    Console.WriteLine($"Failed to parse item ID {itemID}");
                    continue;
                }
                string englishText = englishTextNode.Value;

                Globals.NameIDDictionary[itemID] = englishText;
            }
        }

        /// <summary>
        /// Increment Batch Size
        /// </summary>
        private static void IncrementBatchSize(ref IDbContextTransaction transaction, EdcDbContext edcDbContext, ref int counter, ref int overallCounter, int batchSize)
        {
            if (counter >= batchSize)
            {
                Console.WriteLine($"Commiting batch to db.");
                edcDbContext.SaveChanges();
                transaction.Commit();
                transaction = edcDbContext.Database.BeginTransaction();
                counter = 0;
            }
            else
            {
                overallCounter++;
                Console.Write($"\rTransaction count: {overallCounter} ");
                counter++;
            }
        }

        /// <summary>
        /// Export the Universe to an EntityFramework Database
        /// </summary>
        /// <param name="regionList">list of Regions to export</param>
        private static void ExportUniverseToEfDb(List<Region> regionList)
        {
            Stopwatch dbStopwatch = new Stopwatch();
            int batchSize = 10000;
            int counter = 0;
            int overallCounter = 0;

            dbStopwatch.Start();

            using (var context = new EdcDbContext())
            {
                context.DropAllTables(); // Todo: Remove when no longer useful!!!

                context.Database.EnsureCreated();
                var transaction = context.Database.BeginTransaction();

                foreach (var region in regionList)
                {
                    if (context.Regions.Any(r => r.Id == region.Id))
                    {
                        context.Regions.Update(region);
                    }
                    else
                    {
                        context.Regions.Add(region);
                    }
                    IncrementBatchSize(ref transaction, context, ref counter, ref overallCounter, batchSize);

                    foreach (var constellation in region.Constellations)
                    {
                        if (context.Constellations.Any(c => c.Id == constellation.Id))
                        {
                            context.Constellations.Update(constellation);
                        }
                        else
                        {
                            context.Constellations.Add(constellation);
                        }
                        IncrementBatchSize(ref transaction, context, ref counter, ref overallCounter, batchSize);

                        foreach (var system in constellation.SolarSystems)
                        {
                            if (context.SolarSystems.Any(s => s.Id == system.Id))
                            {
                                context.SolarSystems.Update(system);
                            }
                            else
                            {
                                context.SolarSystems.Add(system);
                            }
                            IncrementBatchSize(ref transaction, context, ref counter, ref overallCounter, batchSize);

                            if (context.Stars.Any(s => s.Id == system.Star.Id))
                            {
                                context.Stars.Update(system.Star);
                            }
                            else
                            {
                                context.Stars.Add(system.Star);
                            }
                            IncrementBatchSize(ref transaction, context, ref counter, ref overallCounter, batchSize);

                            foreach (var planet in system.Planets)
                            {
                                if (context.Planets.Any(p => p.Id == planet.Id))
                                {
                                    context.Planets.Update(planet);
                                }
                                else
                                {
                                    context.Planets.Add(planet);
                                }

                                IncrementBatchSize(ref transaction, context, ref counter, ref overallCounter, batchSize);

                                foreach (var moon in planet.Moons)
                                {
                                    if (context.Moons.Any(m => m.Id == moon.Id))
                                    {
                                        context.Moons.Update(moon);
                                    }
                                    else
                                    {
                                        context.Moons.Add(moon);
                                    }

                                    IncrementBatchSize(ref transaction, context, ref counter, ref overallCounter, batchSize);
                                }
                            }

                            foreach (var station in system.Stations)
                            {
                                if (context.Stations.Any(s => s.Id == station.Id))
                                {
                                    context.Stations.Update(station);
                                }
                                else
                                {
                                    context.Stations.Add(station);
                                }

                                IncrementBatchSize(ref transaction, context, ref counter, ref overallCounter, batchSize);
                            }
                        }
                    }
                }

                context.SaveChanges();
                transaction.Commit();

                dbStopwatch.Stop();
                TimeSpan dbExecutionTime = dbStopwatch.Elapsed;

                Console.WriteLine($"\nDatabase operations: {dbExecutionTime.TotalSeconds.ToString("n2")} seconds");
            }
        }

        // export
        private static void ExportUniverseToDB(string outPutfolder, List<Region> regionList)
        {
            /*
            string dbPath = $"{outPutfolder}\\Universe.db";

            // create from scratch
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }

            using (var dbConnection = new SqliteConnection($"Data Source={dbPath}"))
            {
                dbConnection.Open();

                // create the tables
                using (var command = dbConnection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Regions (
                            Id INTEGER PRIMARY KEY,
                            Name TEXT NOT NULL,
                            FactionId INTEGER
                        );

                        CREATE TABLE IF NOT EXISTS Constellations (
                            Id INTEGER PRIMARY KEY,
                            RegionId INTEGER NOT NULL,
                            Name TEXT NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS SolarSystems (
                            Id INTEGER PRIMARY KEY,
                            ConstellationId INTEGER NOT NULL,
                            Name TEXT NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS Planets (
                            Id INTEGER PRIMARY KEY,
                            SolarSystemId INTEGER NOT NULL,
                            Name TEXT NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS Moons (
                            Id INTEGER PRIMARY KEY,
                            PlanetId INTEGER NOT NULL,
                            Name TEXT NOT NULL
                        );

                        ";
                    command.ExecuteNonQuery();
                }

                using (var transaction = dbConnection.BeginTransaction())
                {
                    // populate the data
                    foreach (var region in regionList)
                    {
                        // Insert data into the various tables
                        using (var command = dbConnection.CreateCommand())
                        {
                            command.CommandText = @$"
                            INSERT INTO Regions (Id, Name, FactionId)
                            VALUES ('{region.Id}', '{region.Name}', '{region.FactionID}')";
                            command.ExecuteNonQuery();
                        }

                        foreach (var constellation in region.Constellations)
                        {
                            // Insert data into the various tables
                            using (var command = dbConnection.CreateCommand())
                            {
                                command.CommandText = @$"
                                INSERT INTO Constellations (Id, Name, RegionId)
                                VALUES ('{constellation.Id}', '{constellation.Name}', '{region.Id}')";
                                command.ExecuteNonQuery();
                            }

                            foreach (var system in constellation.SolarSystems)
                            {
                                // Insert data into the various tables
                                using (var command = dbConnection.CreateCommand())
                                {
                                    command.CommandText = @$"
                                    INSERT INTO SolarSystems (Id, Name, ConstellationId)
                                    VALUES ('{system.Id}', '{system.Name}', '{constellation.Id}')";
                                    command.ExecuteNonQuery();
                                }

                                foreach (var planet in system.Planets)
                                {
                                    // Insert data into the various tables
                                    using (var command = dbConnection.CreateCommand())
                                    {
                                        command.CommandText = @$"
                                        INSERT INTO Planets (Id, Name, SolarSystemId)
                                        VALUES ('{planet.Id}', '{planet.Name}', '{system.Id}')";
                                        command.ExecuteNonQuery();
                                    }

                                    foreach (var moon in planet.Moons)
                                    {
                                        // Insert data into the various tables
                                        using (var command = dbConnection.CreateCommand())
                                        {
                                            command.CommandText = @$"
                                        INSERT INTO Moons (Id, Name, PlanetId)
                                        VALUES ('{moon.Id}', '{moon.Name}', '{planet.Id}')";
                                            command.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    transaction.Commit();
                }
            }
            */
        }
    }
}