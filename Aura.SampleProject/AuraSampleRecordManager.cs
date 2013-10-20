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
            EnsureIndex(IndexKeys.Ascending(PropertyName(x => x.TestField)), IndexOptions.Null);
           // EnsureTextIndex(PropertyName(x => x.TestSearchField));
        }

        public IEnumerable<AuraSampleRecord> SearchForText(string text)
        {
            return TextSearch(text).Select(x => x.Result);
        }
    }
}
