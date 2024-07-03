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
using System.Globalization;
using EveDataCollator.Data;
using Microsoft.EntityFrameworkCore.Storage;

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
                int stationId = YamlParser.ParseYamlValue(e, "stationID", YamlParser.ParseInt);
                int constellationId = YamlParser.ParseYamlValue(e, "constellationID", YamlParser.ParseInt);
                int corporationId = YamlParser.ParseYamlValue(e, "corporationID", YamlParser.ParseInt);
                float dockingCostPerVolume = YamlParser.ParseYamlValue(e, "dockingCostPerVolume", YamlParser.ParseFloat);
                float maxShipVolumeDockable = YamlParser.ParseYamlValue(e, "maxShipVolumeDockable", YamlParser.ParseFloat);
                float officeRentalCost = YamlParser.ParseYamlValue(e, "officeRentalCost", YamlParser.ParseFloat);
                int operationId = YamlParser.ParseYamlValue(e, "operationID", YamlParser.ParseInt);
                int regionId = YamlParser.ParseYamlValue(e, "regionID", YamlParser.ParseInt);
                float reprocessingEfficiency = YamlParser.ParseYamlValue(e, "reprocessingEfficiency", YamlParser.ParseFloat);
                int reprocessingHangarFlag = YamlParser.ParseYamlValue(e, "reprocessingHangarFlag", YamlParser.ParseInt);
                float reprocessingStationsTake = YamlParser.ParseYamlValue(e, "reprocessingStationsTake", YamlParser.ParseFloat);
                float security = YamlParser.ParseYamlValue(e, "security", YamlParser.ParseFloat);
                int solarSystemId = YamlParser.ParseYamlValue(e, "solarSystemId", YamlParser.ParseInt);
                string stationName = YamlParser.ParseYamlValue(e, "stationName", YamlParser.ParseString);
                int stationTypeId = YamlParser.ParseYamlValue(e, "stationTypeID", YamlParser.ParseInt);

                decimal positionX = YamlParser.ParseYamlValue(e, "x", YamlParser.ParseDecimal);
                decimal positionY = YamlParser.ParseYamlValue(e, "y", YamlParser.ParseDecimal);
                decimal positionZ = YamlParser.ParseYamlValue(e, "z", YamlParser.ParseDecimal);

                DecVector3 position = new DecVector3(positionX, positionY, positionZ);
                
                Station station = new Station()
                {
                    Id = stationId,
                    ConstellationId = constellationId,
                    CorporationId = corporationId,
                    DockingCostPerVolume = dockingCostPerVolume,
                    MaxShipVolumeDockable = maxShipVolumeDockable,
                    OfficeRentalCost = officeRentalCost,
                    OperationId = operationId,
                    RegionId = regionId,
                    ReprocessingEfficiency = reprocessingEfficiency,
                    ReprocessingHangarFlag = reprocessingHangarFlag,
                    ReprocessingStationsTake = reprocessingStationsTake,
                    Security = security,
                    SolarSystemId = solarSystemId,
                    StationName = stationName,
                    StationTypeId = stationTypeId,
                    Position = position
                };

                // the stations list contains all stations yet we're currently only parsing K-Space and
                // Thera (J-space)
                if(systems.ContainsKey(solarSystemId))
                {
                    systems[solarSystemId].Stations.Add(station);
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

            int regionId = YamlParser.ParseYamlValue(root, "regionID", YamlParser.ParseInt);
            DecVector3 center = ((YamlSequenceNode)root.Children["center"]).ToDecVector3();
            DecVector3 max = ((YamlSequenceNode)root.Children["max"]).ToDecVector3();
            DecVector3 min = ((YamlSequenceNode)root.Children["min"]).ToDecVector3();
            int descriptionId = YamlParser.ParseYamlValue(root, "descriptionID", YamlParser.ParseInt);
            int nameId = YamlParser.ParseYamlValue(root, "nameID", YamlParser.ParseInt);
            int nebula = YamlParser.ParseYamlValue(root, "nebula", YamlParser.ParseInt);
            int wormholeClassId = YamlParser.ParseYamlValue(root, "wormholeClassID", YamlParser.ParseInt);

            Region r = new Region()
            {
                Id = regionId,
                Name = nameIDDictionary[regionId],
                Center = center,
                DescriptionId = descriptionId,
                FactionId = 0, // Todo: Where does this come from?
                Max = max,
                Min = min,
                NameId = nameId,
                Nebula = nebula,
                WormholeClassId = wormholeClassId,
                Constellations = new List<Constellation>()
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
            
            int constellationId = YamlParser.ParseYamlValue(root, "constellationID", YamlParser.ParseInt);
            int nameId = YamlParser.ParseYamlValue(root, "nameID", YamlParser.ParseInt);
            decimal radius = YamlParser.ParseYamlValue(root, "radius", YamlParser.ParseDecimal);
            DecVector3 center = ((YamlSequenceNode)root.Children["center"]).ToDecVector3();
            DecVector3 max = ((YamlSequenceNode)root.Children["max"]).ToDecVector3();
            DecVector3 min = ((YamlSequenceNode)root.Children["min"]).ToDecVector3();

            Constellation c = new Constellation()
            {
                Id = constellationId,
                Name = nameIDDictionary[constellationId],
                Center = center,
                Max = max,
                Min = min,
                NameId = nameId,
                Radius = radius,
                SolarSystems = new List<SolarSystem>(),
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
            
            int solarSystemId = YamlParser.ParseYamlValue(root, "solarSystemID", YamlParser.ParseInt);
            DecVector3 center = ((YamlSequenceNode)root.Children["center"]).ToDecVector3();
            DecVector3 max = ((YamlSequenceNode)root.Children["max"]).ToDecVector3();
            DecVector3 min = ((YamlSequenceNode)root.Children["min"]).ToDecVector3();
            YamlScalarNode borderNode = (YamlScalarNode)root.Children["border"];
            bool border = YamlParser.ParseYamlValue(root, "border", YamlParser.ParseBool);
            bool corridor = YamlParser.ParseYamlValue(root, "corridor", YamlParser.ParseBool);
            bool fringe = YamlParser.ParseYamlValue(root, "fringe", YamlParser.ParseBool);
            bool hub = YamlParser.ParseYamlValue(root, "hub", YamlParser.ParseBool);
            bool international = YamlParser.ParseYamlValue(root, "international", YamlParser.ParseBool);
            float luminosity = YamlParser.ParseYamlValue(root, "luminosity", YamlParser.ParseFloat);
            decimal radius = YamlParser.ParseYamlValue(root, "radius", YamlParser.ParseDecimal);
            bool regional = YamlParser.ParseYamlValue(root, "regional", YamlParser.ParseBool);
            float security = YamlParser.ParseYamlValue(root, "security", YamlParser.ParseFloat);
            int solarSystemNameId = YamlParser.ParseYamlValue(root, "solarSystemNameID", YamlParser.ParseInt);
            int sunTypeId = YamlParser.ParseYamlValue(root, "sunTypeID", YamlParser.ParseInt);
            int wormholeClassId = YamlParser.ParseYamlValue(root, "wormholeClassID", YamlParser.ParseInt);

            SolarSystem solarSystem = new SolarSystem()
            {
                Id = solarSystemId,
                Name = nameIDDictionary[solarSystemId],
                Border = border,
                Center = center,
                Corridor = corridor,
                DisallowedAnchorCategories = new (), // Todo: What is this and how do we store it?
                Fringe = fringe,
                Hub = hub,
                International = international,
                Luminosity = luminosity,
                Max = max,
                Min = min,
                Planets = new List<Planet>(),
                Radius = radius,
                Regional = regional,
                Security = security,
                SolarSystemNameId = solarSystemNameId,
                //Star is handled below
                Stargates = new (),
                Stations = new (),
                SunTypeId = sunTypeId,
                WormholeClassId = wormholeClassId
            };

            systems[solarSystemId] = solarSystem;

            // Parse the star
            YamlMappingNode starRootNode = (YamlMappingNode)root.Children["star"];
            solarSystem.Star = ParseStarYaml(starRootNode);


            // parse the planets
            YamlMappingNode planetRootNote = (YamlMappingNode)root.Children["planets"];
            foreach (var pn in planetRootNote.Children)
            {
                solarSystem.Planets.Add(ParsePlanetYaml(pn));
            }
            return solarSystem;
        }

        static Planet ParsePlanetYaml(KeyValuePair<YamlNode, YamlNode> planetNode)
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
                
            int planetId = int.Parse((string)planetNode.Key);

            YamlMappingNode planetInfoNode = (YamlMappingNode)planetNode.Value;
            
            int celestialIndex = YamlParser.ParseYamlValue(planetInfoNode, "celestialIndex", YamlParser.ParseInt);
            DecVector3 position = ((YamlSequenceNode)planetInfoNode.Children["position"]).ToDecVector3();
            decimal radius = YamlParser.ParseYamlValue(planetInfoNode, "radius", YamlParser.ParseDecimal);
            int typeId = YamlParser.ParseYamlValue(planetInfoNode, "typeID", YamlParser.ParseInt);

            PlanetAttributes planetAttributes = ParsePlanetAttributesYaml((YamlMappingNode)planetInfoNode.Children["planetAttributes"]);
            Statistics planetStatistics = planetInfoNode.Children.TryGetValue("statistics", out var _) 
                ? ParseStatistics((YamlMappingNode)planetInfoNode.Children["statistics"]) 
                : new ();
            
            Planet planet = new Planet()
            {
                Id = planetId,
                Name = nameIDDictionary[planetId],
                AsteroidBelts = new List<AsteroidBelt>(),
                CelestialIndex = celestialIndex,
                PlanetAttributes = planetAttributes,
                Moons = new List<Moon>(),
                Position = position,
                Radius = radius,
                Statistics = planetStatistics,
                TypeId = typeId
            };
            
            planets[planetId] = planet;
                
            // parse the asteroidBelts
            if (planetInfoNode.Children.Keys.Contains("asteroidBelts"))
            {
                YamlMappingNode asteroidBeltsRootNode = (YamlMappingNode)planetInfoNode.Children["asteroidBelts"];
                foreach (var ab in asteroidBeltsRootNode)
                {
                    planet.AsteroidBelts.Add(ParseAsteroidBeltYaml(ab));
                }
            }

            // parse the moons
            if(planetInfoNode.Children.Keys.Contains("moons"))
            {
                YamlMappingNode moonsRootNode = (YamlMappingNode)planetInfoNode.Children["moons"];
                foreach (var mn in moonsRootNode)
                {
                    planet.Moons.Add(ParseMoonYaml(mn));
                }
            }
            return planet;
        }

        static Statistics ParseStatistics(YamlMappingNode statisticsNode)
        {
            // ToDo: Come up with update strategy for existing statistics
            decimal age = YamlParser.ParseYamlValue(statisticsNode, "age", YamlParser.ParseDecimal);
            float density = YamlParser.ParseYamlValue(statisticsNode, "density", YamlParser.ParseFloat);
            float eccentricity = YamlParser.ParseYamlValue(statisticsNode, "eccentricity", YamlParser.ParseFloat);
            float escapeVelocity = YamlParser.ParseYamlValue(statisticsNode, "escapeVelocity", YamlParser.ParseFloat);
            bool fragmented = YamlParser.ParseYamlValue(statisticsNode, "fragmented", YamlParser.ParseBool);
            float life = YamlParser.ParseYamlValue(statisticsNode, "life", YamlParser.ParseFloat);
            bool locked = YamlParser.ParseYamlValue(statisticsNode, "locked", YamlParser.ParseBool);
            decimal massDust = YamlParser.ParseYamlValue(statisticsNode, "massDust", YamlParser.ParseDecimal);
            decimal massGas = YamlParser.ParseYamlValue(statisticsNode, "massGas", YamlParser.ParseDecimal);
            decimal orbitPeriod = YamlParser.ParseYamlValue(statisticsNode, "orbitPeriod", YamlParser.ParseDecimal);
            decimal orbitRadius = YamlParser.ParseYamlValue(statisticsNode, "orbitRadius", YamlParser.ParseDecimal);
            float pressure = YamlParser.ParseYamlValue(statisticsNode, "pressure", YamlParser.ParseFloat);
            decimal radius = YamlParser.ParseYamlValue(statisticsNode, "radius", YamlParser.ParseDecimal);
            float rotationRate = YamlParser.ParseYamlValue(statisticsNode, "rotationRate", YamlParser.ParseFloat);
            string spectralClass = YamlParser.ParseYamlValue(statisticsNode, "spectralClass", YamlParser.ParseString);
            float surfaceGravity = YamlParser.ParseYamlValue(statisticsNode, "surfaceGravity", YamlParser.ParseFloat);
            float temperature = YamlParser.ParseYamlValue(statisticsNode, "temperature", YamlParser.ParseFloat);
            
            Statistics statistics = new Statistics()
            {
                Age = age,
                Density = density,
                Eccentricity = eccentricity,
                EscapeVelocity = escapeVelocity,
                Fragmented = fragmented,
                Life = life,
                Locked = locked,
                MassDust = massDust,
                MassGas = massGas,
                OrbitPeriod = orbitPeriod,
                OrbitRadius = orbitRadius,
                Pressure = pressure,
                Radius = radius,
                RotationRate = rotationRate,
                SpectralClass = spectralClass,
                SurfaceGravity = surfaceGravity,
                Temperature = temperature
            };

            return statistics;
        }
        
        static PlanetAttributes ParsePlanetAttributesYaml(YamlMappingNode planetAttributesNode)
        {
            // ToDo: Come up with update strategy for existing planet attributes
            
            int heightMap1 = YamlParser.ParseYamlValue(planetAttributesNode, "heightMap1", YamlParser.ParseInt);
            int heightMap2 = YamlParser.ParseYamlValue(planetAttributesNode, "heightMap2", YamlParser.ParseInt);
            bool population = YamlParser.ParseYamlValue(planetAttributesNode, "population", YamlParser.ParseBool);
            int shaderPreset = YamlParser.ParseYamlValue(planetAttributesNode, "shaderPreset", YamlParser.ParseInt);

            PlanetAttributes planetAttributes = new PlanetAttributes()
            {
                HeightMap1 = heightMap1,
                HeightMap2 = heightMap2,
                Population = population,
                ShaderPreset = shaderPreset
            };
            
            return planetAttributes;
        }

        // parse a star
        static Star ParseStarYaml(YamlMappingNode starNode)
        {
            // Stars are part of the solarsystem YAML and the format is:
            // radius
            // statistics
            // typeID
            
            int starId = YamlParser.ParseYamlValue(starNode, "id", YamlParser.ParseInt);
            decimal radius = YamlParser.ParseYamlValue(starNode, "radius", YamlParser.ParseDecimal);
            int typeId = YamlParser.ParseYamlValue(starNode, "typeID", YamlParser.ParseInt);
            
            Statistics starStatistics = starNode.Children.TryGetValue("statistics", out var _) 
                ? ParseStatistics((YamlMappingNode)starNode.Children["statistics"]) 
                : new ();

            Star star = new Star()
            {
                Id = starId,
                Radius = radius,
                Statistics = starStatistics,
                TypeId = typeId,
                Power = 0 // Todo: Where does this come from?
            };

            stars[starId] = star;

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
                        
            int moonId = int.Parse((string)moonNode.Key);
            YamlMappingNode moonInfoNode = (YamlMappingNode)moonNode.Value;

            DecVector3 position = ((YamlSequenceNode)moonInfoNode.Children["position"]).ToDecVector3();
            decimal radius = YamlParser.ParseYamlValue(moonInfoNode, "radius", YamlParser.ParseDecimal);
            int typeId = YamlParser.ParseYamlValue(moonInfoNode, "typeID", YamlParser.ParseInt);
            
            Statistics moonStatistics = moonInfoNode.Children.TryGetValue("statistics", out var _) 
                ? ParseStatistics((YamlMappingNode)moonInfoNode.Children["statistics"]) 
                : new ();
            

            Moon moon = new Moon()
            {
                Id = moonId,
                Name = nameIDDictionary[moonId],
                PlanetAttributes = new (),
                Position = position,
                Radius = radius,
                Statistics = moonStatistics,
                TypeId = typeId
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

            int asteroidBeltId = int.Parse((string)asteroidBeltNode.Key);
            YamlMappingNode asteroidBeltInfoNode = (YamlMappingNode)asteroidBeltNode.Value;
            
            DecVector3 position = ((YamlSequenceNode)asteroidBeltInfoNode.Children["position"]).ToDecVector3();
            int typeId = YamlParser.ParseYamlValue(asteroidBeltInfoNode, "typeID", YamlParser.ParseInt);
            
            Statistics asteroidBeltStatistics = asteroidBeltInfoNode.Children.TryGetValue("statistics", out var _) 
                ? ParseStatistics((YamlMappingNode)asteroidBeltInfoNode.Children["statistics"]) 
                : new ();

            AsteroidBelt asteroidBelt = new AsteroidBelt()
            {
                Id = asteroidBeltId,
                Position = position,
                Statistics = asteroidBeltStatistics,
                TypeId = typeId
            };

            return asteroidBelt;
        }

        static void IncrementBatchSize(ref IDbContextTransaction transaction, EdcDbContext edcDbContext, ref int counter, ref int overallCounter, int batchSize)
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
        
        static void ExportUniverseToEfDb(List<Region> regionList)
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
        static void ExportUniverseToDB(string outPutfolder, List<Region> regionList)
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
