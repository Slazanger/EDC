using System.IO.Compression;
using YamlDotNet.RepresentationModel;
using EveDataCollator.Eve;
using System.Numerics;
using System.Diagnostics;
using System;


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


            // check the current SDE
            string currentSDEChecksum = await GetCurrentSDECheckSum(checksumUrl);


            // check if we already have this
            string dataFolder = $"{System.AppContext.BaseDirectory}{currentSDEChecksum}";
            string SDELocal = $"{dataFolder}\\sde.zip";
            bool downloadSDE = false;

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

            // if we dont have the local file, download it
            if(downloadSDE)
            {
                // download latest SDE
                await DownloadSDE(SDEUrl, SDELocal);

                // extract SDE zip
                ZipFile.ExtractToDirectory(SDELocal, dataFolder, true);
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
        static async Task<string> GetCurrentSDECheckSum(string checksumUrl)
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
                int planetID = int.Parse((string)pn.Key);

                YamlMappingNode planetInfoNode = (YamlMappingNode)pn.Value;

                YamlScalarNode typeIDNode = (YamlScalarNode)planetInfoNode.Children["typeID"];
                int planetTypeID = int.Parse(typeIDNode.Value);

                Planet p = new Planet()
                {
                    Id = planetID,
                    Name = nameIDDictionary[planetID],
                    TypeId = planetTypeID,
                    Moons = new List<Moon>()
                };
                s.Planets.Add(p);

                // parse the moons
                if(planetInfoNode.Children.Keys.Contains("moons"))
                {
                    YamlMappingNode moonsRootNode = (YamlMappingNode)planetInfoNode.Children["moons"];
                    foreach (var mn in moonsRootNode)
                    {
                        int moonID = int.Parse((string)mn.Key);
                        YamlMappingNode moonInfoNode = (YamlMappingNode)pn.Value;

                        YamlScalarNode moonTypeIDNode = (YamlScalarNode)moonInfoNode.Children["typeID"];
                        int moonTypeID = int.Parse(moonTypeIDNode.Value);

                        Moon moon = new Moon()
                        {
                            Id = moonID,
                            Name = nameIDDictionary[moonID],
                            TypeId = moonTypeID
                        };

                        p.Moons.Add(moon);

                    }
                }
            }
            return s;
        }
    }
}
