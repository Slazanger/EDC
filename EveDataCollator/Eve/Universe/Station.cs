﻿using System.ComponentModel.DataAnnotations;
using EveDataCollator.Data;

namespace EveDataCollator.EVE.Universe
{
    public class Station
    {
        // constellationID
        // corporationID
        // dockingCostPerVolume
        // maxShipVolumeDockable
        // officeRentalCost
        // operationID
        // regionID
        // reprocessingEfficiency
        // reprocessingHangarFlag
        // reprocessingStationsTake
        // security
        // solarSystemID
        // stationID
        // stationName
        // stationTypeID
        // x
        // y
        // z

        public int Id { get; set; }
        public int ConstellationId { get; set; }
        public int CorporationId { get; set; }
        public float DockingCostPerVolume { get; set; }
        public float MaxShipVolumeDockable { get; set; }
        public float OfficeRentalCost { get; set; }
        public int OperationId { get; set; }
        public int RegionId { get; set; }
        public float ReprocessingEfficiency { get; set; }
        public int ReprocessingHangarFlag { get; set; }
        public float ReprocessingStationsTake { get; set; }
        public float Security { get; set; }
        public int SolarSystemId { get; set; }

        [StringLength(128)]
        public string StationName { get; set; } = string.Empty;

        public int StationTypeId { get; set; }
        public DecVector3 Position;
    }
}