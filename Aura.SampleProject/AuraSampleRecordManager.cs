using MongoDB.Driver.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura.SampleProject
{
    class AuraSampleRecordManager : RecordManager<AuraSampleRecord>
    {
        public AuraSampleRecordManager()
            : base(true)
        {

        }

        protected override void Initialize()
        {
            EnsureIndex(IndexKeys.Ascending(PropertyName(x => x.TestField)), IndexOptions.SetUnique(false).SetSparse(false));
        }
    }
}
