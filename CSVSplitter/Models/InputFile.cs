using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CSVSplitter;
using CsvHelper;
using CsvHelper.Configuration;

namespace CSVSplitter.Models
{
    public class InputFile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public string _filePath;

        public string FilePath 
        {
            get
            {
                return _filePath;
            }
            set
            {
                _filePath = value;
                NotifyPropertyChanged();
            }
        }

        private bool _isTextFile = false;

        private char _delimiter;

        private bool _hasBom;

        private bool _isAnalyzed = false;

        private Encoding _encoding = null;

        private string _newLine = null;

        private string _rawHeader;

        private string[] _header;

        private bool _isCsvFile = false;

        public char Delimiter
        {
            get
            {
                return _delimiter;
            }
            private set
            {
                _delimiter = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("DelimiterName");
            }
        }

        public string DelimiterName
        {
            get
            {
                if (this.Delimiter == '\t')
                {
                    return "タブ";
                }
                else
                {
                    return this.Delimiter.ToString();
                }
            }
        }


        public bool HasBom
        {
            get
            {
                return _hasBom;
            }
            private set
            {
                _hasBom = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("HasBomName");
            }
        }

        public string HasBomName
        {
            get
            {
                return this.HasBom ? "あり" : "なし";
            }
        }

        public bool IsAnalyzed
        { 
            get 
            { 
                return _isAnalyzed;
            }
            private set
            {
                _isAnalyzed = value;
                NotifyPropertyChanged();
            }
        }

        public bool IsTextFile
        {
            get
            {
                return _isTextFile;
            }
            private set
            {
                _isTextFile = value;
                NotifyPropertyChanged();
            }
        }

        public Encoding Encoding
        {
            get
            {
                return _encoding;
            }
            private set
            {
                _encoding = value;
                NotifyPropertyChanged();
            }
        }

        public string NewLine
        {
            get
            {
                return _newLine;
            }
            private set
            {
                _newLine = value;
                NotifyPropertyChanged();
            }
        }

        public string RawHeader
        {
            get
            {
                return _rawHeader;
            }
            private set
            {
                _rawHeader = value;
                NotifyPropertyChanged();
            }
        }

        public string[] Header
        {
            get
            {
                return _header;
            }
            private set
            {
                _header = value;
                NotifyPropertyChanged();
            }
        }

        public bool IsCsvFile
        {
            get
            {
                return _isCsvFile;
            }
            private set
            {
                _isCsvFile = value;
                NotifyPropertyChanged();
            }
        }

        public void Analyze()
        {
            if(!File.Exists(this.FilePath))
            {
                this.IsAnalyzed = false;
                this.IsTextFile = false;
                this.Encoding = null;
                return;
            }

            this.Encoding = GetEncoding(this.FilePath);
            if (this._encoding is null)
            {
                this.IsAnalyzed = true;
                this.IsTextFile = false;
                return;
            }

            this.IsTextFile = true;

            using (var fs = new FileStream(this.FilePath, FileMode.Open))
            {
                byte[] buffer = new byte[4];
                fs.Read(buffer, 0, buffer.Length);
                this.HasBom = CheckBom(buffer);
            }

            this.Encoding = ApplyBomSetting(this.Encoding, this.HasBom);

            using (var reader = new StreamReader(this.FilePath, this.Encoding))
            {
                this.Delimiter = '\0';

                // とりあえず、一行目を読み込む
                var header = reader.ReadLine();
                this.RawHeader = header;

                int conmaCount = header.Count(c => c == ',');
                int tabCount = header.Count(c => c == '\t');

                if (conmaCount > tabCount)
                {
                    this.Delimiter = ',';
                }
                else if (conmaCount < tabCount)
                {
                    this.Delimiter = '\t';
                }
            }

            // 改行コードの判定をする
            using (var reader = new StreamReader(this.FilePath, this.Encoding))
            {
                const int maxReadSize = 512 * 1024;
                int readSize = 0;

                const char CR = '\r';
                const char LF = '\n';
                char nowChar = (char)0;
                bool dqFlag = false;
                this.NewLine = null;
                while (!reader.EndOfStream)
                {
                    char nextChar = (char)reader.Read();
                    if (nextChar == '"')
                    {
                        dqFlag = !dqFlag;
                    }
                    if (nowChar == CR && nextChar == LF)
                    {
                        this.NewLine = CR.ToString() + LF.ToString();
                        if (!dqFlag)
                        {
                            break;
                        }
                    }
                    else if (nowChar == CR)
                    {
                        this.NewLine = CR.ToString();
                        if (!dqFlag)
                        {
                            break;
                        }
                    }
                    else if (nowChar == LF)
                    {
                        this.NewLine = LF.ToString();
                        if (!dqFlag)
                        {
                            break;
                        }
                    }
                    nowChar = nextChar;
                    readSize++;
                    if (readSize > maxReadSize)
                    {
                        break;
                    }
                }
            }

            this.IsCsvFile = this.IsTextFile && this.Delimiter != '\0' && this.NewLine != null && this.Encoding != null;
            this.Header = GetColName().ToArray();

            this.IsAnalyzed = true;

        }

        private System.Text.Encoding GetEncoding(string filename)
        {
            int maxSize = 512 * 1024;
            var file = new System.IO.FileInfo(filename);
            System.Text.Encoding result = null;

            if (maxSize > file.Length)
            {
                using (Hnx8.ReadJEnc.FileReader reader = new Hnx8.ReadJEnc.FileReader(file))
                {
                    Hnx8.ReadJEnc.CharCode code = reader.Read(file);
                    var tmpResult = code.GetEncoding();

                    if (tmpResult != null)
                    {
                        result = tmpResult;
                    }

                }
            }
            else
            {
                // 512KBまで読み込む
                byte[] buffer = new byte[maxSize];
                using (var fs = file.OpenRead())
                {
                    fs.Read(buffer, 0, buffer.Length);
                }
                if (!(buffer is null))
                {
                    string tmpEncResult = null;
                    const int maxLoop = 30;

                    for (int i = 0; i < maxLoop; i++)
                    {
                        byte[] tmpBytes = new byte[buffer.Length - i];
                        Array.Copy(buffer, i, tmpBytes, 0, buffer.Length - i);

                        Hnx8.ReadJEnc.CharCode code = Hnx8.ReadJEnc.ReadJEnc.JP.GetEncoding(tmpBytes, tmpBytes.Length, out tmpEncResult);
                        if (tmpEncResult != null)
                        {
                            result = code.GetEncoding();
                            break;
                        }
                    }
                }
            }

            return result;
        }

        private bool CheckBom(byte[] buffer)
        {
            if (buffer.Length < 3)
            {
                return false;
            }

            // UTF-8
            if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                return true;
            }

            // UTF-16 BE
            if (buffer[0] == 0xFE && buffer[1] == 0xFF)
            {
                return true;
            }

            // UTF-16 LE
            if (buffer[0] == 0xFF && buffer[1] == 0xFE)
            {
                return true;
            }

            if (buffer.Length < 4)
            {
                return false;
            }

            // UTF-32 BE
            if (buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF)
            {
                return true;
            }

            // UTF-32 LE
            if (buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
            {
                return true;
            }

            return false;
        }

        private System.Text.Encoding ApplyBomSetting(System.Text.Encoding enc, bool flgBom)
        {
            System.Text.Encoding result = enc;

            if (flgBom)
            {
                if (enc is System.Text.UTF8Encoding)
                {
                    result = new System.Text.UTF8Encoding(true);
                }
                else if (enc is System.Text.UnicodeEncoding)
                {
                    if (enc.CodePage == 1200)
                    {
                        result = new System.Text.UnicodeEncoding(false, true);
                    }
                    else if (enc.CodePage == 1201)
                    {
                        result = new System.Text.UnicodeEncoding(true, true);
                    }
                }
                else if (enc is System.Text.UTF32Encoding)
                {
                    if (enc.CodePage == 12000)
                    {
                        result = new System.Text.UTF32Encoding(false, true);
                    }
                    else if (enc.CodePage == 12001)
                    {
                        result = new System.Text.UTF32Encoding(true, true);
                    }
                }
            }
            else
            {
                if (enc is System.Text.UTF8Encoding)
                {
                    result = new System.Text.UTF8Encoding(false);
                }
                else if (enc is System.Text.UnicodeEncoding)
                {
                    if (enc.CodePage == 1200)
                    {
                        result = new System.Text.UnicodeEncoding(false, false);
                    }
                    else if (enc.CodePage == 1201)
                    {
                        result = new System.Text.UnicodeEncoding(true, false);
                    }
                }
                else if (enc is System.Text.UTF32Encoding)
                {
                    if (enc.CodePage == 12000)
                    {
                        result = new System.Text.UTF32Encoding(false, false);
                    }
                    else if (enc.CodePage == 12001)
                    {
                        result = new System.Text.UTF32Encoding(true, false);
                    }
                }
            }

            return result;
        }

        public CsvConfiguration GetCsvConfig()
        {
            if (!this.IsCsvFile)
            {
                return null;
            }

            var config = new CsvConfiguration(System.Globalization.CultureInfo.CurrentCulture)
            {
                Delimiter = this.Delimiter.ToString(),
                HasHeaderRecord = true,
                IgnoreBlankLines = true,
                NewLine = this.NewLine,
                Encoding = this.Encoding
            };

            return config;
        }

        public List<string> GetColName()
        {
            if (!this.IsCsvFile)
            {
                return new List<string>();
            }

            var config = GetCsvConfig();
            var headerList = new List<string>();
            using (var reader = new StreamReader(this.FilePath, this.Encoding))
            {
                using (var csv = new CsvReader(reader, config))
                {
                    while(csv.Read())
                    {
                        var record = csv.GetRecord<dynamic>() as IDictionary<string,object>;
                        Utils.DebugTool.WriteLine("Raw: " + csv.Context.Parser.RawRecord);
                        foreach(var data in record)
                        {
                            if(!headerList.Contains(data.Key))
                            {
                                Utils.DebugTool.WriteLine("data.Key" + data.Key);
                                Utils.DebugTool.WriteLine("data.Value" + data.Value);
                                headerList.Add(data.Key);
                            }
                        }
                        break;
                    }
                }
            }

            return headerList;
        }
    }
}
