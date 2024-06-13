﻿using System.IO.Compression;
using YamlDotNet.RepresentationModel;
using EveDataCollator.Eve;
using System.Numerics;
using System.Diagnostics;
using System;
using System.Security.Cryptography;


namespace EveDataCollator
{
    internal class Program
    {
        static private Dictionary<int, string> nameIDDictionary;
        static private Dictionary<int, Region> regions;


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

            //
            Console.WriteLine($"Parsed {regions.Count} regions");
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


        // Parse Universe files
        static void ParseUniverse(string rootFolder)
        {
            regions = new Dictionary<int, Region>();

            // universe is in .\universe\eve\<region>\<constellation>\<system>


            // regions
            var matchingRegionFiles = Directory.EnumerateFiles(rootFolder, "region.yaml", SearchOption.AllDirectories);
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

            SolarSystem s = new SolarSystem()
            {
                Id = solarSystemID,
                Name = nameIDDictionary[solarSystemID],
                Planets = new List<Planet>()
            };

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
                s.Planets.Add(p);
                
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
            return s;
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
    }
}
