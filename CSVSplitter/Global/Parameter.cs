using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSVSplitter.Global
{
    public class Parameter
    {
        //MAX_SORTFILE_RECORDS
        public static long GetMaxSortFileRecords(int headerCount)
        {
            Utils.DebugTool.WriteLine("GetMaxSortFileRecords");
            long tmp = Global.Const.DEFAULT_SORTFILE_RECORDS / headerCount * 20;
            if(tmp > Global.Const.MAX_SORTFILE_RECORDS)
            {
                tmp = Global.Const.MAX_SORTFILE_RECORDS;
            }
            else if(tmp < Global.Const.MIN_SORTFILE_RECORDS)
            {
                tmp = Global.Const.MIN_SORTFILE_RECORDS;
            }

            Utils.DebugTool.WriteLine("GetMaxSortFileRecords: " + tmp);
            return tmp;
        }
    }
}
