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
        public const int PARALLEL_SORT_THRESHOLD = 20_000;
        public const int PARALLEL_SORT_THRESHOLD_MIN = 20_000;
        public const int PARALLEL_SORT_THRESHOLD_MAX = 200_000;
        public const int PARALLEL_SORT_THRESHOLD_PER_CORE = 8_000;
        public const int IO_READ_BUFFER_SIZE = 128 * 1024;
        public const int IO_WRITE_BUFFER_SIZE = 128 * 1024;
    }
}
