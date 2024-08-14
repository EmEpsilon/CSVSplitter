using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CSVSplitter.Models
{
    public class InputFiles : ObservableCollection<InputFile>
    {
        public bool _hasUniformHeaders;
        public bool HasUniformHeaders
        {
            get
            {
                return _hasUniformHeaders;
            }
            private set
            {
                _hasUniformHeaders = value;
                this.OnPropertyChanged(new PropertyChangedEventArgs("HasUniformHeaders"));
            }
        }

        public void Add(string filePath)
        {
            var inputFile = new InputFile();
            inputFile.FilePath = filePath;
            this.Add(inputFile);
        }
        public bool Contains(string filePath)
        {
            return this.Any(f => f.FilePath == filePath);
        }

        public bool AreAllTextFiles()
        {
            return this.All(f => f.IsTextFile);
        }

        public bool AreAllCsvFiles()
        {
            return this.All(f => f.IsCsvFile);
        }

        public bool AreAllAnalyzed()
        {
            return this.All(f => f.IsAnalyzed);
        }

        public void Analyze()
        {
            foreach (var file in this)
            {
                file.Analyze();
            }

            string topHeader = "";
            Encoding topEncoding = null;
            bool hasUniformHeaders = true;
            foreach (var file in this)
            {
                if (topHeader == "")
                {
                    topHeader = file.RawHeader;
                    topEncoding = file.Encoding;
                }
                else
                {
                    if (topHeader != file.RawHeader)
                    {
                        hasUniformHeaders = false;
                        break;
                    }
                    else if (topEncoding.CodePage != file.Encoding.CodePage)
                    {
                        hasUniformHeaders = false;
                        break;
                    }
                }
            }

            this.HasUniformHeaders = hasUniformHeaders;
        }

        public string GetAnalysisReport()
        {
            StringBuilder sb = new StringBuilder();

            if(!AreAllAnalyzed())
            {
                sb.AppendLine("すべてのファイルが解析されていません。");
            }
            
            if (!AreAllCsvFiles())
            {
                sb.AppendLine("CSVではないファイルがあります。");
            }

            if (sb.Length != 0)
            {
                sb.AppendLine("分割・出力できないため、リセットボタンを押し、対象外のファイルを除いて取り込みなおしてください。");
            }
            else
            {
                if (this.Count > 1)
                {
                    if (this.HasUniformHeaders)
                    {
                        sb.AppendLine("すべてのファイルのヘッダーと文字コードが一致しています。複数ファイルの統合ができます。");
                    }
                    else
                    {
                        sb.AppendLine("すべてのファイルのヘッダーと文字コードが一致していません。ファイルごとの分割を実施できます。");
                    }
                }

                sb.AppendLine("分割・出力できます。");
            }

            return sb.ToString();

        }

        public List<string> GetCommonHeaders()
        {
            var commonHeaders = new List<string>();
            if (this.Count > 0 )
            {
                commonHeaders = new List<string>(this[0].Header);
            }

            foreach (var file in this)
            {
                if (file.IsAnalyzed && file.IsCsvFile)
                {
                    commonHeaders = commonHeaders.Intersect(file.Header).ToList();
                }
            }

            return commonHeaders;
        }
    }
}
