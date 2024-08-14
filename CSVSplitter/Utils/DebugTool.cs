using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CSVSplitter.Utils
{
    internal class DebugTool
    {
        private static long _counter = 0;

        private static long Counter
        {
            get 
            { 
                return ++_counter; 
            }
        }

        public static void WriteLine(string message, bool datetimeMode = true, [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0, [CallerMemberName] string propertyName = "")
        {
#if DEBUG
            long count = 0;
            count = Counter;
            string countStr = count.ToString().PadLeft(10, '0');
            Debug.WriteLine("■■■■■■■■■■■");
            Debug.WriteLine("■ " + countStr + " " + DateTime.Now.ToString());
            Debug.WriteLine("■ {0} sourceFilePath: {1}, sourceLineNumber: {2}, propertyName: {3}", countStr, sourceFilePath, sourceLineNumber,propertyName);
            Debug.WriteLine("■ " + countStr + " message ===> " + message);
            Debug.WriteLine("■■■■■■■■■■■");
#endif
        }
    }
}
