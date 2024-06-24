using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveDataCollator.Eve
{
    public class Station
    {
        public int Id {  get; set; }
        [StringLength(128)]
        public string Name { get; set; }
        public int TypeId {  get; set; }

    }
}
