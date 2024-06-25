using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EveDataCollator.Data;

namespace EveDataCollator.Eve
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
            
        public int Id {  get; set; }
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
        public string StationName { get; set; } = String.Empty;
        public int StationTypeId {  get; set; }
        public DecVector3 Position;

    }
}
