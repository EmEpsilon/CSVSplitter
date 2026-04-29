using CsvHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSVSplitter.Models
{
    public class SortCsvRow
    {
        public SortCsvRow()
        {
        }

        public string RawData { get; set; }
        public IDictionary<string, string> Data { get; set; }
        public SortKey[] SortKeyArray { get; set; }
        public bool isSettedKey { get; set; } = false;
        public void SetSortKey(SortComparer comp)
        {
            SortKeyArray = comp.BuildSortKeys(this.Data);
            this.isSettedKey = true;
        }

        public void SetSortKey(SortComparer comp, string[] recordValues, int[] optionIndexes)
        {
            SortKeyArray = comp.BuildSortKeys(recordValues, optionIndexes);
            this.isSettedKey = true;
        }
    }

    public struct SortKey
    {
        public object obj;
        public double num;
    }
}
