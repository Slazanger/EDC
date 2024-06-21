using System.Diagnostics;
using System.IO.Compression;
using YamlDotNet.RepresentationModel;
using EveDataCollator.Eve;
using System.Security.Cryptography;
using System.Timers;
using Microsoft.Data.Sqlite;
using System.Transactions;
using EveDataCollator.EDCEF;
using System.Diagnostics;

namespace EveDataCollator
{
    internal class Program
    {
        static private Dictionary<int, string> nameIDDictionary = default;
        static private Dictionary<int, Region> regions = default;
        static private Dictionary<int, Planet> planets = default;
        static private Dictionary<int, Star> stars = default;
        static private Dictionary<int, SolarSystem> systems = default;

        static async Task Main(string[] args)
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
            
            if(downloadSDE)
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
                            string destinationDirectory = Path.GetDirectoryName(destinationPath);
                            if (!Directory.Exists(destinationDirectory))
                            {
                                Directory.CreateDirectory(destinationDirectory);
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
                        string hashString = BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();
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
            
            // collate all the universe files
            ParseUniverse(dataFolder);


            // get all of the NPC stations
            ParseNPCStations(dataFolder);


            // extract the planet/star power/workforce data
            ParsePlanetResources(dataFolder);


            Console.WriteLine($"Parsed {regions.Count} regions");


            //ExportUniverseToDB(dataFolder, regions.Values.ToList());
            ExportUniverseToEfDb(regions.Values.ToList());

        }


        // get the current SDE Checksum from the 
        static async Task<string> GetSdeCheckSumFromServer(string checksumUrl)
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


        // Get the latest SDE from CCP
        static async Task DownloadSDE(string SDEUrl, string localFile)
        {
            Console.WriteLine($"Downloading Latest SDE : {SDEUrl}");
            await DownloadFile(SDEUrl, localFile);
            Console.WriteLine($"Downloaded to {localFile}");

        }
       

        // download file
        static async Task DownloadFile(string fileUrl, string localFile)
        {
            using var httpClient = new HttpClient();
            using var responseStream = await httpClient.GetStreamAsync(fileUrl);
            using var fileStream = new FileStream(localFile, FileMode.OpenOrCreate);
            await responseStream.CopyToAsync(fileStream);
        }


        // parse and create the id to name dictionary
        static void LoadNameDictionary(string rootFolder)
        {
            nameIDDictionary = new Dictionary<int, string>();

            string dictionaryFile = $"{rootFolder}\\bsd\\invNames.yaml";

            using var sr = new StreamReader(dictionaryFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlSequenceNode)yamlStream.Documents[0].RootNode;


            foreach (var e in root.Children)
            {
                YamlScalarNode itemIDNode = (YamlScalarNode)e["itemID"];
                int itemID = int.Parse(itemIDNode.Value);

                YamlScalarNode englishTextNode = (YamlScalarNode)e["itemName"];
                string englishText = englishTextNode.Value;

                nameIDDictionary[itemID] = englishText;
            }
        }


        // parse the planet resources
        static void ParsePlanetResources(string rootFolder)
        {
            nameIDDictionary = new Dictionary<int, string>();

            string planetResourceFile = $"{rootFolder}\\fsd\\planetResources.yaml";

            using var sr = new StreamReader(planetResourceFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;


            foreach (var e in root.Children)
            {
                YamlScalarNode idNode = (YamlScalarNode)e.Key;
                int planetID = int.Parse(idNode.Value);

                YamlMappingNode planetNode = (YamlMappingNode)e.Value;

                int power = 0;
                if(planetNode.Children.ContainsKey("power"))
                {
                    YamlScalarNode powerNode = (YamlScalarNode)planetNode["power"];
                    power = int.Parse(powerNode.Value);
                }

                int workforce = 0;
                if (planetNode.Children.ContainsKey("workforce"))
                {
                    YamlScalarNode workforceNode = (YamlScalarNode)planetNode["workforce"];
                    workforce = int.Parse(workforceNode.Value);
                }

                // is it a planet ?
                if(planets.ContainsKey(planetID))
                {
                    planets[planetID].Workforce = workforce;
                }

                // is it a star ?
                if(stars.ContainsKey(planetID))
                {
                    stars[planetID].Power = power;
                }
            }
        }


        // parse the planet resources
        static void ParseNPCStations(string rootFolder)
        {
            nameIDDictionary = new Dictionary<int, string>();

            string stationResourceFile = $"{rootFolder}\\bsd\\staStations.yaml";

            using var sr = new StreamReader(stationResourceFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlSequenceNode)yamlStream.Documents[0].RootNode;

            foreach (var e in root.Children)
            {
                YamlScalarNode stationIDNode = (YamlScalarNode)e["stationID"];
                int stationID = int.Parse(stationIDNode.Value);

                YamlScalarNode solarSystemIDNode = (YamlScalarNode)e["solarSystemID"];
                int solarSystemID = int.Parse(solarSystemIDNode.Value);


                YamlScalarNode stationNameNode = (YamlScalarNode)e["stationName"];
                string stationName = stationNameNode.Value;

                Station station = new Station()
                {
                    Id = stationID,
                    Name = stationName
                };

                // the stations list contains all stations yet we're currently only parsing K-Space and
                // Thera (J-space)
                if(systems.ContainsKey(solarSystemID))
                {
                    systems[solarSystemID].Stations.Add(station);
                }

            }
        }


        // Parse Universe files
        static void ParseUniverse(string rootFolder)
        {
            regions = new Dictionary<int, Region>();
            planets = new Dictionary<int, Planet>();
            stars = new Dictionary<int, Star>();
            systems = new Dictionary<int, SolarSystem>();



            // universe is in .\universe\eve\<region>\<constellation>\<system>
            string universeRoot = rootFolder + @"\universe\eve";


            // regions
            var matchingRegionFiles = Directory.EnumerateFiles(universeRoot, "region.yaml", SearchOption.AllDirectories);
            foreach (string regionFile in matchingRegionFiles)
            {
                Region r = ParseRegionYaml(regionFile);
                regions[r.Id] = r; 


                // get the constellations within this folder
                string regionDir = Path.GetDirectoryName(regionFile);

                // constellations
                // regions
                var matchingConstellationFiles = Directory.EnumerateFiles(regionDir, "constellation.yaml", SearchOption.AllDirectories);
                foreach (string constellationFile in matchingConstellationFiles)
                {
                    Constellation c = ParseConstellationYaml(constellationFile);
                    r.Constellations.Add(c);


                    // get the systems within this folder
                    string constellationDir = Path.GetDirectoryName(constellationFile);

                    // constellations
                    // regions
                    var matchingSystemFiles = Directory.EnumerateFiles(constellationDir, "solarSystem.yaml", SearchOption.AllDirectories);
                    foreach (string systemFile in matchingSystemFiles)
                    {
                        SolarSystem s = ParseSolarSystemYaml(systemFile);
                        c.SolarSystems.Add(s);
                    }
                }
            }
        }


        // Parse the region
        static Region ParseRegionYaml(string yamlFile)
        {
            // The region YAML is in the format : 
            // center
            // - X,Y,Z
            // descriptionID
            // max
            // - X,Y,Z
            // min:
            // - X,Y,Z
            // nameID
            // nebula
            // regionID
            // wormholeClassID


            using var sr = new StreamReader(yamlFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

            YamlScalarNode regionIDNode = (YamlScalarNode)root.Children["regionID"];
            int regionID = int.Parse(regionIDNode.Value);

            Region r = new Region()
            {
                Name = nameIDDictionary[regionID],
                Id = regionID,
                Constellations = new List<Constellation>(),
            };

            return r;

        }


        // Parse the constellation
        static Constellation ParseConstellationYaml(string yamlFile)
        {
            // The constellation YAML is in the format : 
            // center
            //     - X,Y,Z
            // constellationID
            // max
            //     -X,Y,Z
            // min
            //     -X,Y,Z
            // nameID
            // radius

            using var sr = new StreamReader(yamlFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

            YamlScalarNode constellationIDNode = (YamlScalarNode)root.Children["constellationID"];
            int constellationID = int.Parse(constellationIDNode.Value);

            Constellation c = new Constellation()
            {
                Id = constellationID,
                Name = nameIDDictionary[constellationID],
                SolarSystems = new List<SolarSystem>()
            };

            return c;
        }


        // Parse the system
        static SolarSystem ParseSolarSystemYaml(string yamlFile)
        {
            // The solarsystem YAML is in the format :
            // border
            // center
            //      - X,Y,Z
            // corridor
            // fringe
            // hub
            // international
            // luminosity
            // max
            //      - X,Y,Z
            // min
            //      - X,Y,Z
            // planets
            // radius
            // regional
            // security
            // solarSystemID
            // solarSystemNameID
            // star
            // stargates
            // sunTypeID
            // wormholeClassID
            
            using var sr = new StreamReader(yamlFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

            YamlScalarNode solarSystemIDNode = (YamlScalarNode)root.Children["solarSystemID"];
            int solarSystemID = int.Parse(solarSystemIDNode.Value);

            SolarSystem solarSystem = new SolarSystem()
            {
                Id = solarSystemID,
                Name = nameIDDictionary[solarSystemID],
                Planets = new List<Planet>(),
                Stations = new List<Station>()
            };

            systems[solarSystemID] = solarSystem;

            // Parse the star
            YamlMappingNode starRootNode = (YamlMappingNode)root.Children["star"];
            solarSystem.Sun = ParseStarYaml(starRootNode);


            // parse the planets
            YamlMappingNode planetRootNote = (YamlMappingNode)root.Children["planets"];
            foreach (var pn in planetRootNote.Children)
            {
                // Planets are part of the solarsystem YAML and the format is:
                // asteroidBelts
                // celestialIndex
                // planetAttributes
                // moons
                // position
                //      - X,Y,Z
                // radius
                // statistics
                // typeID
                
                int planetID = int.Parse((string)pn.Key);

                YamlMappingNode planetInfoNode = (YamlMappingNode)pn.Value;

                YamlScalarNode typeIDNode = (YamlScalarNode)planetInfoNode.Children["typeID"];
                int planetTypeID = int.Parse(typeIDNode.Value);

                Planet p = new Planet()
                {
                    Id = planetID,
                    Name = nameIDDictionary[planetID],
                    TypeId = planetTypeID,
                    AsteroidBelts = new List<AsteroidBelt>(),
                    Moons = new List<Moon>()
                };
                solarSystem.Planets.Add(p);

                planets[planetID] = p;
                
                // parse the asteroidBelts
                if (planetInfoNode.Children.Keys.Contains("asteroidBelts"))
                {
                    YamlMappingNode asteroidBeltsRootNode = (YamlMappingNode)planetInfoNode.Children["asteroidBelts"];
                    foreach (var ab in asteroidBeltsRootNode)
                    {
                        p.AsteroidBelts.Add(ParseAsteroidBeltYaml(ab));
                    }
                }

                // parse the moons
                if(planetInfoNode.Children.Keys.Contains("moons"))
                {
                    YamlMappingNode moonsRootNode = (YamlMappingNode)planetInfoNode.Children["moons"];
                    foreach (var mn in moonsRootNode)
                    {
                        p.Moons.Add(ParseMoonYaml(mn));
                    }
                }
            }
            return solarSystem;
        }

        // parse a moon
        static Star ParseStarYaml(YamlMappingNode starNode)
        {
            // Stars are part of the solarsystem YAML and the format is:
            // radius
            // statistics
            // typeID

            YamlScalarNode starIDNode = (YamlScalarNode)starNode.Children["id"];
            int starID = int.Parse(starIDNode.Value);

            YamlScalarNode starTypeIDNode = (YamlScalarNode)starNode.Children["typeID"];
            int starTypeID = int.Parse(starTypeIDNode.Value);

            Star star = new Star()
            {
                Id = starID,
                TypeId = starTypeID
            };

            stars[starID] = star;

            return star;
        }


        // parse a moon
        static Moon ParseMoonYaml(KeyValuePair<YamlNode, YamlNode> moonNode)
        {
            // Moons are part of the solarsystem/planets YAML and the format is:
            // planetAttributes
            // position
            //      - X,Y,Z
            // radius
            // statistics
            // typeID
                        
            int moonID = int.Parse((string)moonNode.Key);
            YamlMappingNode moonInfoNode = (YamlMappingNode)moonNode.Value;

            YamlScalarNode moonTypeIDNode = (YamlScalarNode)moonInfoNode.Children["typeID"];
            int moonTypeID = int.Parse(moonTypeIDNode.Value);

            Moon moon = new Moon()
            {
                Id = moonID,
                Name = nameIDDictionary[moonID],
                TypeId = moonTypeID
            };

            return moon;
        }
        
        // parse an asteroidBelt
        static AsteroidBelt ParseAsteroidBeltYaml(KeyValuePair<YamlNode, YamlNode> asteroidBeltNode)
        {
            // AsteroidBelts are part of the solarsystem/planets YAML and the format is:
            // position
            //      - X,Y,Z
            // statistics
            // typeID

            int asteroidBeltID = int.Parse((string)asteroidBeltNode.Key);
            YamlMappingNode asteroidBeltInfoNode = (YamlMappingNode)asteroidBeltNode.Value;

            YamlScalarNode asteroidBeltTypeIDNode = (YamlScalarNode)asteroidBeltInfoNode.Children["typeID"];
            int asteroidBeltTypeID = int.Parse(asteroidBeltTypeIDNode.Value);

            AsteroidBelt asteroidBelt = new AsteroidBelt()
            {
                Id = asteroidBeltID,
                TypeId = asteroidBeltTypeID
            };

            return asteroidBelt;
        }

        static void ExportUniverseToEfDb(List<Region> regionList)
        {
            Stopwatch dbStopwatch = new Stopwatch();
            
            dbStopwatch.Start();
            
            using (var context = new EdcDbContext())
            {
                context.Database.EnsureCreated();
                context.Database.BeginTransaction();
                
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

                            if (context.Stars.Any(s => s.Id == system.Sun.Id))
                            {
                                context.Stars.Update(system.Sun);
                            }
                            else
                            {
                                context.Stars.Add(system.Sun);
                            }

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
                                }
                            }

                            foreach (var station in system.Stations)
                            {
                                if (context.Stations.Any(p => p.Id == station.Id))
                                {
                                    context.Stations.Update(station);
                                }
                                else
                                {
                                    context.Stations.Add(station);
                                } 
                            }
                        }
                    }
                }
                context.Database.CommitTransaction();
                context.SaveChanges();
                
                dbStopwatch.Stop();
                TimeSpan dbExecutionTime = dbStopwatch.Elapsed;
                
                Console.WriteLine($"Database operations: {dbExecutionTime.TotalSeconds.ToString("n2")} seconds");
            }
        }

        // export 
        static void ExportUniverseToDB(string outPutfolder, List<Region> regionList)
        {
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
        }
    }
}
