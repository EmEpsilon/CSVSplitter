using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSVSplitter.Utils
{
    public class Utils
    {
        public static string GetAssemblyPath()
        {
            if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
            {
                return AppContext.BaseDirectory;
            }

            return Environment.CurrentDirectory;
        }

        
    }
}
