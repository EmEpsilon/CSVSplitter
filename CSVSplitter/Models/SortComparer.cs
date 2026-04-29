using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;

namespace CSVSplitter.Models
{
    public class SortComparer : IComparer<SortCsvRow>
    {
        public List<SortOption> Options { get; set; }
        public SortComparer(List<SortOption> prmOptions) 
        {
            this.Options = prmOptions;
        }
        public int Compare(SortCsvRow x, SortCsvRow y)
        {
            return CompareCore(x.Data, y.Data, x.SortKeyArray, y.SortKeyArray, x.isSettedKey, y.isSettedKey);
        }

        public SortKey[] BuildSortKeys(IDictionary<string, string> data)
        {
            if (this.Options.Count == 0)
            {
                return Array.Empty<SortKey>();
            }

            var sortKeys = new SortKey[this.Options.Count];
            for (int i = 0; i < this.Options.Count; i++)
            {
                var option = this.Options[i];
                SortKey key = new SortKey();
                if (option.IsNumeric)
                {
                    double num;
                    if (!data.TryGetValue(option.ColName, out var valueText))
                    {
                        valueText = null;
                    }
                    if (!double.TryParse(valueText, out num))
                    {
                        num = 0;
                    }
                    key.num = num;
                }
                else
                {
                    if (!data.TryGetValue(option.ColName, out var valueText))
                    {
                        valueText = null;
                    }
                    key.obj = valueText ?? "";
                }
                sortKeys[i] = key;
            }
            return sortKeys;
        }

        public int CompareRecords(
            IDictionary<string, string> xData,
            SortKey[] xSortKeys,
            bool xHasSortKeys,
            IDictionary<string, string> yData,
            SortKey[] ySortKeys,
            bool yHasSortKeys)
        {
            return CompareCore(xData, yData, xSortKeys, ySortKeys, xHasSortKeys, yHasSortKeys);
        }

        private int CompareCore(
            IDictionary<string, string> xData,
            IDictionary<string, string> yData,
            SortKey[] xSortKeys,
            SortKey[] ySortKeys,
            bool xHasSortKeys,
            bool yHasSortKeys)
        {
            int i = 0;
            foreach(SortOption option in this.Options)
            {
                int result = 0;
                if (xHasSortKeys && yHasSortKeys)
                {
                    if (option.IsNumeric)
                    {
                        result = xSortKeys[i].num.CompareTo(ySortKeys[i].num);
                    }
                    else
                    {
                        result = String.Compare(xSortKeys[i].obj?.ToString(), ySortKeys[i].obj?.ToString(), StringComparison.Ordinal);
                    }
                    if (result != 0)
                    {
                        return option.Descending ? -result : result;
                    }
                }
                else
                {
                    if (option.IsNumeric)
                    {
                        double tmp1;
                        double tmp2;
                        if (!xData.TryGetValue(option.ColName, out var xValue))
                        {
                            xValue = null;
                        }
                        if (!yData.TryGetValue(option.ColName, out var yValue))
                        {
                            yValue = null;
                        }
                        if (!double.TryParse(xValue, out tmp1))
                        {
                            tmp1 = 0;
                        }
                        if (!double.TryParse(yValue, out tmp2))
                        {
                            tmp2 = 0;
                        }
                        result = tmp1.CompareTo(tmp2);
                    }
                    else
                    {
                        if (!xData.TryGetValue(option.ColName, out var xValue))
                        {
                            xValue = null;
                        }
                        if (!yData.TryGetValue(option.ColName, out var yValue))
                        {
                            yValue = null;
                        }
                        result = String.Compare(xValue, yValue, StringComparison.Ordinal);
                    }
                    if (result != 0)
                    {
                        return option.Descending ? -result : result;
                    }
                }
                i++;
            }
            return 0;
        }
        public bool isEmpty()
        {
            return this.Options.Count == 0;
        }
    }

    public class SortOption
    {
        public string ColName { get; set; }
        public bool Descending { get; set; }
        public bool IsNumeric { get; set; }
        public SortOption(string prmColName,bool prmDesc,bool prmIsNumeric) 
        {
            this.ColName = prmColName;
            this.Descending = prmDesc;
            this.IsNumeric = prmIsNumeric;
        }
    }
}
