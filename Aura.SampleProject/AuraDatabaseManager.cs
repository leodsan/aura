using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura.SampleProject
{
    class AuraDatabaseManager : DatabaseManager
    {
        public AuraSampleRecordManager RecordManager { get; set; }
        public RecordManager<AuraSampleRecord> RecordManager2 { get; set; }
    }
}
