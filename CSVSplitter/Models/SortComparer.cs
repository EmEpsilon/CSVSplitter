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
        private List<SortOption> Options { get; set; }
        public SortComparer(List<SortOption> prmOptions) 
        {
            this.Options = prmOptions;
        }
        public int Compare(SortCsvRow x, SortCsvRow y)
        {
            foreach(SortOption option in this.Options)
            {
                int result = 0;
                if (option.IsNumeric)
                {
                    double tmp1;
                    double tmp2;
                    if (!double.TryParse(x.Data[option.ColName].ToString(), out tmp1))
                    {
                        tmp1 = 0;
                    }
                    if (!double.TryParse(y.Data[option.ColName].ToString(), out tmp2))
                    {
                        tmp2 = 0;
                    }
                    result = tmp1.CompareTo(tmp2);
                }
                else
                {
                    result = String.Compare(x.Data[option.ColName].ToString(), y.Data[option.ColName].ToString());
                }
                if (result != 0)
                {
                    return option.Descending ? -result : result;
                }
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
