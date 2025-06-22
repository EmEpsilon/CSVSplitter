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
        public IDictionary<string, object> Data { get; set; }
        public SortKey[] SortKeyArray { get; set; }
        public bool isSettedKey { get; set; } = false;
        public void SetSortKey(SortComparer comp)
        {
            SortKeyArray = new SortKey[comp.Options.Count];
            int i = 0;
            foreach (SortOption option in comp.Options)
            {
                SortKey key = new SortKey();
                if (option.IsNumeric)
                {
                    double num;
                    if (!double.TryParse(this.Data[option.ColName].ToString(), out num))
                    {
                        num = 0;
                    }
                    key.num = num;
                }
                else
                {
                    key.obj = this.Data[option.ColName].ToString();
                }
                this.SortKeyArray[i] = key;
                i++;
            }
            this.isSettedKey = true;
        }
    }

    public struct SortKey
    {
        public object obj;
        public double num;
    }
}
