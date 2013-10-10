using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura.SampleProject
{
    class Program
    {
        static void Main(string[] args)
        {
            AuraDatabaseManager manager = new AuraDatabaseManager();
            manager.RecordManager.Save(new AuraSampleRecord { TestField = "test", TestSearchField = "foo"});
            manager.RecordManager.Save(new AuraSampleRecord { TestField = "test 2", TestSearchField = "bar" });
            var documents = manager.RecordManager.SearchForText("bar");
        }
    }
}
