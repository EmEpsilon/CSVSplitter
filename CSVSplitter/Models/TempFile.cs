using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CSVSplitter.Models
{
    public class TempFile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private string _filePath;
        private Guid _guid;

        public string FilePath
        {
            get
            {
                return _filePath;
            }
            private set
            {
                _filePath = value;
                NotifyPropertyChanged();
            }
        }

        public Guid Guid
        {
            get
            {
                return _guid;
            }
            private set
            {
                _guid = value;
                NotifyPropertyChanged();
            }
        }

        public TempFile()
        {
            Guid = Guid.NewGuid();
            FilePath = Path.Combine( System.IO.Path.GetTempPath(), Guid.ToString() + ".tmp");
            FileStream fs = File.Create(FilePath);
            fs.Close();
            Utils.DebugTool.WriteLine("TempFile created: " + FilePath + " :: " + Guid.ToString());
        }

        public void Delete()
        {
            if (FilePath != "" && System.IO.File.Exists(FilePath))
            {
                System.IO.File.Delete(FilePath);
                FilePath = "";
                Utils.DebugTool.WriteLine("TempFile deleted: " + FilePath + " :: " + Guid.ToString());
            }
            this.FilePath = "";
        }
    }
}
