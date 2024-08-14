using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSVSplitter.Global
{
    public class Const
    {
        public const long MAX_SORTFILE_RECORDS = 100_000;
        public const long DEFAULT_SORTFILE_RECORDS = 20_000;
        public const long MIN_SORTFILE_RECORDS = 2_000;
        public const long DEFAULT_MAX_SPLITFILE_RECORDS = 1_000_000;
        public const long WRITE_BUFFER_SIZE = 500;
    }
}
