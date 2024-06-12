using System.Net;
using System;
using System.Net.Http;
using System.IO.Compression;
using YamlDotNet.RepresentationModel;
using System.Security.Cryptography.X509Certificates;

namespace EveDataCollator
{
    internal class Program
    {
        static private Dictionary<int, string> nameIDDictionary;


        static async Task Main(string[] args)
        {
            string SDEUrl = @"https://eve-static-data-export.s3-eu-west-1.amazonaws.com/tranquility/sde.zip";
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string dataFolder = $"{System.AppContext.BaseDirectory}{timestamp}";
            string SDELocal = $"{dataFolder}/sde.zip";

            // TODO : make this less developer specific
            bool skipDL = true;
            if(skipDL)
            {
                dataFolder = $"{System.AppContext.BaseDirectory}dev";
                SDELocal = $"{dataFolder}\\sde.zip";
            }
            else
            {
                // create the download directory
                Directory.CreateDirectory(timestamp);

                // download latest SDE
                await DownloadSDE(SDEUrl, SDELocal);

                // extract SDE zip
                ZipFile.ExtractToDirectory(SDELocal, dataFolder);
            }

            // load the string database
            LoadNameDictionary(dataFolder);

            // collate all the universe files
            ParseUniverse(dataFolder);
        }


        // Get the latest SDE from CCP
        static async Task DownloadSDE(string SDEUrl, string localFile)
        {
            Console.WriteLine($"Downloading Latest SDE : {SDEUrl}");

            using var httpClient = new HttpClient();
            using var responseStream = await httpClient.GetStreamAsync(SDEUrl);
            using var fileStream = new FileStream(localFile, FileMode.CreateNew);
            await responseStream.CopyToAsync(fileStream);

            Console.WriteLine($"Downloaded to {localFile}");
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
            // universe is in .\universe\eve\<region>\<constellation>\<system>


            // regions
            var matchingRegionFiles = Directory.EnumerateFiles(rootFolder, "region.yaml", SearchOption.AllDirectories);
            foreach (string regionFile in matchingRegionFiles)
            {
                ParseRegionYaml(regionFile);

                // get the constellations within this folder
                string regionDir = Path.GetDirectoryName(regionFile);

                // constellations
                // regions
                var matchingConstellationFiles = Directory.EnumerateFiles(regionDir, "constellation.yaml", SearchOption.AllDirectories);
                foreach (string constellationFile in matchingConstellationFiles)
                {
                    ParseConstellationYaml(constellationFile);

                    // get the systems within this folder
                    string constellationDir = Path.GetDirectoryName(constellationFile);

                    // constellations
                    // regions
                    var matchingSystemFiles = Directory.EnumerateFiles(constellationDir, "solarSystem.yaml", SearchOption.AllDirectories);
                    foreach (string systemFile in matchingSystemFiles)
                    {
                        ParseSystemYaml(systemFile);
                    }
                }
            }
        }


        // Parse the region
        static void ParseRegionYaml(string yamlFile)
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

            Console.WriteLine($"Found Region : {nameIDDictionary[regionID]} ({regionID})");
        }


        // Parse the constellation
        static void ParseConstellationYaml(string yamlFile)
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

            Console.WriteLine($"    - Found constellation : {nameIDDictionary[constellationID]} ({constellationID})");
        }


        // Parse the system
        static void ParseSystemYaml(string yamlFile)
        {
            using var sr = new StreamReader(yamlFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

            YamlScalarNode solarSystemIDNode = (YamlScalarNode)root.Children["solarSystemID"];
            int solarSystemID = int.Parse(solarSystemIDNode.Value);

            Console.WriteLine($"        - Found System : {nameIDDictionary[solarSystemID]} ({solarSystemID})");
        }
    }
}
