using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.ObjectModel;

namespace CSVSplitter.Models
{
    public class TempFiles : ObservableCollection<TempFile>,IDisposable
    {
        private string _tempFolder;
        public string TempFolder
        {
            get
            {
                return _tempFolder;
            }
            private set
            {
                _tempFolder = value;
            }
        }

        public TempFiles()
        {
        }

        public string GetTempFilePath(Guid guid )
        {
            string tempFilePath = string.Empty;
            foreach (var tempFile in this)
            {
                if (tempFile.Guid == guid)
                {
                    tempFilePath = tempFile.FilePath;
                    break;
                }
            }

            return tempFilePath;
        }

        public TempFile Add()
        {
            var tempFile = new TempFile();
            this.Add(tempFile);
            return tempFile;
        }

        public void Delete(Guid guid)
        {
            var tempFile = this.FirstOrDefault(f => f.Guid == guid);
            if (tempFile != null)
            {
                tempFile.Delete();
                this.Remove(tempFile);
            }
        }

        public void DeleteAll()
        {
            foreach (var tempFile in this)
            {
                tempFile.Delete();
            }
            this.Clear();
        }

        public void Dispose()
        {
            foreach (var tempFile in this)
            {
                tempFile.Delete();
            }
        }

    }
}
