using System.Linq;
using EveDataCollator.Data;
using EveDataCollator.EDCEF;
using EveDataCollator.EVE.Universe;
using YamlDotNet.RepresentationModel;

namespace EveDataCollator
{
    public class Universe
    {
        public static Dictionary<int, Region> Regions { get; set; }
        public static Dictionary<int, Planet> Planets { get; set; }
        public static Dictionary<int, Star> Stars { get; set; }
        public static Dictionary<int, SolarSystem> Systems { get; set; }


        static Universe()
        {
            Regions = [];
            Planets = [];
            Stars = [];
            Systems = [];
        }
            


        /// <summary>
        /// Parse the main universe YAML data, its various compents are in the .\universe folder of the SDE
        /// </summary>
        /// <param name="rootFolder">The extracted root folder of the SDE</param>
        public static void Parse(string rootFolder)
        {
            Regions.Clear();
            Planets.Clear();
            Stars.Clear();
            Systems.Clear();

            // universe is in .\universe\eve\<region>\<constellation>\<system>
            string universeRoot = rootFolder + @"\universe\eve";

            // Regions
            var matchingRegionFiles = Directory.EnumerateFiles(universeRoot, "region.yaml", SearchOption.AllDirectories);
            foreach (string regionFile in matchingRegionFiles)
            {
                Region r = ParseRegionYaml(regionFile);
                Regions[r.Id] = r;

                // get the constellations within this folder
                string? regionDir = Path.GetDirectoryName(regionFile);

                if(regionDir == null)
                {
                    Console.WriteLine($"Error Parsing {regionFile}");
                }

                // constellations
                // Regions
                var matchingConstellationFiles = Directory.EnumerateFiles(regionDir!, "constellation.yaml", SearchOption.AllDirectories);
                foreach (string constellationFile in matchingConstellationFiles)
                {
                    Constellation c = ParseConstellationYaml(constellationFile);
                    r.Constellations.Add(c);

                    // get the Systems within this folder
                    string? constellationDir = Path.GetDirectoryName(constellationFile);

                    if(constellationDir == null)
                    {
                        Console.WriteLine($"Failed to extract directory for {constellationFile}");
                        continue;
                    }

                    // constellations
                    // Regions
                    var matchingSystemFiles = Directory.EnumerateFiles(constellationDir, "solarSystem.yaml", SearchOption.AllDirectories);
                    foreach (string systemFile in matchingSystemFiles)
                    {
                        SolarSystem s = ParseSolarSystemYaml(systemFile);
                        c.SolarSystems.Add(s);
                    }
                }
            }

            // After the main universe structure has been parsed we can parse the additional data

            // get all of the NPC stations
            ParseNPCStations(rootFolder);

            // extract the planet/star power/workforce data
            ParsePlanetResources(rootFolder);
        }

        // parse the planet resources
        private static void ParsePlanetResources(string rootFolder)
        {
            Globals.NameIDDictionary = [];

            string planetResourceFile = $"{rootFolder}\\fsd\\planetResources.yaml";

            using var sr = new StreamReader(planetResourceFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(sr);

            var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

            foreach (var e in root.Children)
            {
                YamlScalarNode idNode = (YamlScalarNode)e.Key;

                int planetID = -1;
                if (idNode.Value != null)
                {
                    int.Parse(idNode.Value);
                }

                YamlMappingNode planetNode = (YamlMappingNode)e.Value;

                int power = 0;
                if (planetNode.Children.ContainsKey("power"))
                {
                    YamlScalarNode powerNode = (YamlScalarNode)planetNode["power"];
                    if (powerNode.Value != null)
                    {
                        power = int.Parse(powerNode.Value);
                    }
                }

                int workforce = 0;
                if (planetNode.Children.ContainsKey("workforce"))
                {
                    YamlScalarNode workforceNode = (YamlScalarNode)planetNode["workforce"];
                    if (workforceNode.Value != null)
                    {
                        workforce = int.Parse(workforceNode.Value);
                    }
                }

                // is it a planet ?
                if (Planets.ContainsKey(planetID))
                {
                    Planets[planetID].Workforce = workforce;
                }

                // is it a star ?
                if (Stars.ContainsKey(planetID))
                {
                    Stars[planetID].Power = power;
                }
            }
        }

        // parse the planet resources
        private static void ParseNPCStations(string rootFolder)
        {
            Globals.NameIDDictionary = [];

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
                string stationName = YamlParser.ParseYamlValue(e, "stationName", YamlParser.ParseString)!;
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
                if (Systems.ContainsKey(solarSystemId))
                {
                    Systems[solarSystemId].Stations.Add(station);
                }
            }
        }

        // Parse the region
        private static Region ParseRegionYaml(string yamlFile)
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
                Name = Globals.NameIDDictionary[regionId],
                Center = center,
                DescriptionId = descriptionId,
                FactionId = 0, // Todo: Where does this come from?
                Max = max,
                Min = min,
                NameId = nameId,
                Nebula = nebula,
                WormholeClassId = wormholeClassId,
                Constellations = []
            };

            return r;
        }

        // Parse the constellation
        private static Constellation ParseConstellationYaml(string yamlFile)
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
                Name = Globals.NameIDDictionary[constellationId],
                Center = center,
                Max = max,
                Min = min,
                NameId = nameId,
                Radius = radius,
                SolarSystems = [],
            };

            return c;
        }

        // Parse the system
        private static SolarSystem ParseSolarSystemYaml(string yamlFile)
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
            // Planets
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
                Name = Globals.NameIDDictionary[solarSystemId],
                Border = border,
                Center = center,
                Corridor = corridor,
                DisallowedAnchorCategories = [], // Todo: What is this and how do we store it?
                Fringe = fringe,
                Hub = hub,
                International = international,
                Luminosity = luminosity,
                Max = max,
                Min = min,
                Planets = [],
                Radius = radius,
                Regional = regional,
                Security = security,
                SolarSystemNameId = solarSystemNameId,
                //Star is handled below
                Stargates = [],
                Stations = [],
                SunTypeId = sunTypeId,
                WormholeClassId = wormholeClassId
            };

            Systems[solarSystemId] = solarSystem;

            // Parse the star
            YamlMappingNode starRootNode = (YamlMappingNode)root.Children["star"];
            solarSystem.Star = ParseStarYaml(starRootNode);

            // parse the Planets
            YamlMappingNode planetRootNote = (YamlMappingNode)root.Children["planets"];
            if (planetRootNote != null)
            {
                foreach (var pn in planetRootNote.Children)
                {
                    solarSystem.Planets.Add(ParsePlanetYaml(pn));
                }
            }
            else
            {
                Console.WriteLine($"{solarSystem.Name} has no planets");
            }
            return solarSystem;
        }

        private static Planet ParsePlanetYaml(KeyValuePair<YamlNode, YamlNode> planetNode)
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

            int planetId = int.Parse((string)planetNode.Key!);

            YamlMappingNode planetInfoNode = (YamlMappingNode)planetNode.Value;

            int celestialIndex = YamlParser.ParseYamlValue(planetInfoNode, "celestialIndex", YamlParser.ParseInt);
            DecVector3 position = ((YamlSequenceNode)planetInfoNode.Children["position"]).ToDecVector3();
            decimal radius = YamlParser.ParseYamlValue(planetInfoNode, "radius", YamlParser.ParseDecimal);
            int typeId = YamlParser.ParseYamlValue(planetInfoNode, "typeID", YamlParser.ParseInt);

            PlanetAttributes planetAttributes = ParsePlanetAttributesYaml((YamlMappingNode)planetInfoNode.Children["planetAttributes"]);
            Statistics planetStatistics = planetInfoNode.Children.TryGetValue("statistics", out var _)
                ? ParseStatistics((YamlMappingNode)planetInfoNode.Children["statistics"])
                : new();

            Planet planet = new Planet()
            {
                Id = planetId,
                Name = Globals.NameIDDictionary[planetId],
                AsteroidBelts = [],
                CelestialIndex = celestialIndex,
                PlanetAttributes = planetAttributes,
                Moons = [],
                Position = position,
                Radius = radius,
                Statistics = planetStatistics,
                TypeId = typeId
            };

            Planets[planetId] = planet;

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
            if (planetInfoNode.Children.Keys.Contains("moons"))
            {
                YamlMappingNode moonsRootNode = (YamlMappingNode)planetInfoNode.Children["moons"];
                foreach (var mn in moonsRootNode)
                {
                    planet.Moons.Add(ParseMoonYaml(mn));
                }
            }
            return planet;
        }

        private static Statistics ParseStatistics(YamlMappingNode statisticsNode)
        {
            // ToDo: Come up with update strategy for existing statistics
            decimal age = YamlParser.ParseYamlValue(statisticsNode, "age", YamlParser.ParseDecimal);
            float density = YamlParser.ParseYamlValue(statisticsNode, "density", YamlParser.ParseFloat);
            float eccentricity = YamlParser.ParseYamlValue(statisticsNode, "eccentricity", YamlParser.ParseFloat);
            float escapeVelocity = YamlParser.ParseYamlValue(statisticsNode, "escapeVelocity", YamlParser.ParseFloat);
            bool fragmented = YamlParser.ParseYamlValue(statisticsNode, "fragmented", YamlParser.ParseBool);
            float life = YamlParser.ParseYamlValue(statisticsNode, "life", YamlParser.ParseFloat);
            bool locked = YamlParser.ParseYamlValue(statisticsNode, "locked", YamlParser.ParseBool);
            float massDust = YamlParser.ParseYamlValue(statisticsNode, "massDust", YamlParser.ParseFloat);
            float massGas = YamlParser.ParseYamlValue(statisticsNode, "massGas", YamlParser.ParseFloat);
            decimal orbitPeriod = YamlParser.ParseYamlValue(statisticsNode, "orbitPeriod", YamlParser.ParseDecimal);
            decimal orbitRadius = YamlParser.ParseYamlValue(statisticsNode, "orbitRadius", YamlParser.ParseDecimal);
            float pressure = YamlParser.ParseYamlValue(statisticsNode, "pressure", YamlParser.ParseFloat);
            decimal radius = YamlParser.ParseYamlValue(statisticsNode, "radius", YamlParser.ParseDecimal);
            float rotationRate = YamlParser.ParseYamlValue(statisticsNode, "rotationRate", YamlParser.ParseFloat);
            string spectralClass = YamlParser.ParseYamlValue(statisticsNode, "spectralClass", YamlParser.ParseString)!;
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

        private static PlanetAttributes ParsePlanetAttributesYaml(YamlMappingNode planetAttributesNode)
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
        private static Star ParseStarYaml(YamlMappingNode starNode)
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
                : new();

            Star star = new Star()
            {
                Id = starId,
                Radius = radius,
                Statistics = starStatistics,
                TypeId = typeId,
                Power = 0 // Todo: Where does this come from?
            };

            Stars[starId] = star;

            return star;
        }

        // parse a moon
        private static Moon ParseMoonYaml(KeyValuePair<YamlNode, YamlNode> moonNode)
        {
            // Moons are part of the solarsystem/Planets YAML and the format is:
            // planetAttributes
            // position
            //      - X,Y,Z
            // radius
            // statistics
            // typeID

            int moonId = int.Parse((string)moonNode.Key!);
            YamlMappingNode moonInfoNode = (YamlMappingNode)moonNode.Value;

            DecVector3 position = ((YamlSequenceNode)moonInfoNode.Children["position"]).ToDecVector3();
            decimal radius = YamlParser.ParseYamlValue(moonInfoNode, "radius", YamlParser.ParseDecimal);
            int typeId = YamlParser.ParseYamlValue(moonInfoNode, "typeID", YamlParser.ParseInt);

            Statistics moonStatistics = moonInfoNode.Children.TryGetValue("statistics", out var _)
                ? ParseStatistics((YamlMappingNode)moonInfoNode.Children["statistics"])
                : new();

            Moon moon = new Moon()
            {
                Id = moonId,
                Name = Globals.NameIDDictionary[moonId],
                PlanetAttributes = new(),
                Position = position,
                Radius = radius,
                Statistics = moonStatistics,
                TypeId = typeId
            };

            return moon;
        }

        // parse an asteroidBelt
        private static AsteroidBelt ParseAsteroidBeltYaml(KeyValuePair<YamlNode, YamlNode> asteroidBeltNode)
        {
            // AsteroidBelts are part of the solarsystem/Planets YAML and the format is:
            // position
            //      - X,Y,Z
            // statistics
            // typeID

            int asteroidBeltId = int.Parse((string)asteroidBeltNode.Key!);
            YamlMappingNode asteroidBeltInfoNode = (YamlMappingNode)asteroidBeltNode.Value;

            DecVector3 position = ((YamlSequenceNode)asteroidBeltInfoNode.Children["position"]).ToDecVector3();
            int typeId = YamlParser.ParseYamlValue(asteroidBeltInfoNode, "typeID", YamlParser.ParseInt);

            Statistics asteroidBeltStatistics = asteroidBeltInfoNode.Children.TryGetValue("statistics", out var _)
                ? ParseStatistics((YamlMappingNode)asteroidBeltInfoNode.Children["statistics"])
                : new();

            AsteroidBelt asteroidBelt = new AsteroidBelt()
            {
                Id = asteroidBeltId,
                Position = position,
                Statistics = asteroidBeltStatistics,
                TypeId = typeId
            };

            return asteroidBelt;
        }
    }
}