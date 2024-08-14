using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using CsvHelper;
using CsvHelper.Configuration;
using CSVSplitter.Models;

namespace CSVSplitter.Commands
{
    public class ConvertCommand : ICommand
    {
        private ViewModels.MainWindowViewModel _viewModel;

        public ConvertCommand(ViewModels.MainWindowViewModel viewModel)
        {
            this._viewModel = viewModel;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return
                    this._viewModel.InputFiles.Count > 0
                && !this._viewModel.IsProcessing
                && this._viewModel.InputFiles.AreAllAnalyzed()
                && this._viewModel.InputFiles.AreAllCsvFiles();
        }

        private List<string> outputFiles; // 非同期メソッド間で共有するための変数
        private Dictionary<string, long> DicCountCsvFile;

        public async void Execute(object parameter)
        {
            Utils.DebugTool.WriteLine("Execute Convert");
            this._viewModel.ChangeProcessingStatus(true);
            this._viewModel.ProgressValue = 0;
            this._viewModel.ResetStopwatch();
            this._viewModel.StartStopwatch();

            var listSortOptions = new List<Models.SortOption>();
            if(!string.IsNullOrEmpty(this._viewModel.SortItem1SelectedValue))
            {
                listSortOptions.Add(new Models.SortOption(this._viewModel.SortItem1SelectedValue, this._viewModel.SortItem1IsDescending,this._viewModel.SortItem1IsNumeric));
            }
            if (!string.IsNullOrEmpty(this._viewModel.SortItem2SelectedValue))
            {
                listSortOptions.Add(new Models.SortOption(this._viewModel.SortItem2SelectedValue, this._viewModel.SortItem2IsDescending, this._viewModel.SortItem2IsNumeric));
            }
            if (!string.IsNullOrEmpty(this._viewModel.SortItem3SelectedValue))
            {
                listSortOptions.Add(new Models.SortOption(this._viewModel.SortItem3SelectedValue, this._viewModel.SortItem3IsDescending, this._viewModel.SortItem3IsNumeric));
            }
            if (!string.IsNullOrEmpty(this._viewModel.SortItem4SelectedValue))
            {
                listSortOptions.Add(new Models.SortOption(this._viewModel.SortItem4SelectedValue, this._viewModel.SortItem4IsDescending, this._viewModel.SortItem4IsNumeric));
            }

            var comp = new Models.SortComparer(listSortOptions);

            var splitInfo = new CCSplitInfo();
            splitInfo.AddHeader(this._viewModel.SplitItem1SelectedValue);
            splitInfo.AddHeader(this._viewModel.SplitItem2SelectedValue);
            splitInfo.AddHeader(this._viewModel.SplitItem3SelectedValue);
            splitInfo.AddHeader(this._viewModel.SplitItem4SelectedValue);

            if(!System.IO.Directory.Exists(this._viewModel.OutputFolder))
            {
                System.IO.Directory.CreateDirectory(this._viewModel.OutputFolder);
            }

            this.outputFiles = new List<string>();
            this.DicCountCsvFile = new Dictionary<string, long>();

            long totalRecords = 0;
            foreach (var file in this._viewModel.InputFiles)
            {
                totalRecords += await CountCsvFileAsync(file.FilePath, file.GetCsvConfig());
            }
            long currentRecords = 0;

            var ccTempFiles = new List<CCTempFile>();
            if (_viewModel.IntegrationMode)
            {
                Utils.DebugTool.WriteLine("IntegrationMode");
                var listMiddleTempFIles = new List<CCTempFile>();
                var ccTempFile = new CCTempFile(this._viewModel.TempFiles.Add());
                ccTempFile.CsvConfig = this._viewModel.InputFiles[0].GetCsvConfig();
                ccTempFile.RawHeader = this._viewModel.InputFiles[0].RawHeader;
                ccTempFile.originalFilePath = this._viewModel.InputFiles[0].FilePath;
                ccTempFiles.Add(ccTempFile);
                listMiddleTempFIles.Add(ccTempFile);

                await MergeCsvFileAsync(this._viewModel.InputFiles.Select(f => f.FilePath).ToList(), ccTempFile.FilePath, ccTempFile.CsvConfig, comp, ccTempFile.RawHeader);
                var ccTempFile2 = new CCTempFile(this._viewModel.TempFiles.Add());
                ccTempFile2.CsvConfig = ccTempFile.CsvConfig;
                ccTempFile2.RawHeader = ccTempFile.RawHeader;
                ccTempFile2.originalFilePath = ccTempFile.originalFilePath;
                ccTempFiles.Add(ccTempFile2);
                listMiddleTempFIles.Add(ccTempFile2);

                var maxSortFileRecords = Global.Parameter.GetMaxSortFileRecords(this._viewModel.InputFiles[0].Header.Length);
                currentRecords += await SortCsvFileAsync(ccTempFile.FilePath, ccTempFile2.FilePath, ccTempFile.CsvConfig, comp, ccTempFile.RawHeader, maxSortFileRecords);
                this._viewModel.ProgressValue = (int)((double)currentRecords / (double)totalRecords * 100) / 2;

                var outputFilePath = Path.Combine(this._viewModel.OutputFolder, Path.GetFileName(ccTempFile2.originalFilePath));
                currentRecords += await OutputCsvFileAsync(ccTempFile2.FilePath, outputFilePath, ccTempFile2.CsvConfig, splitInfo, ccTempFile2.RawHeader);
                this._viewModel.ProgressValue = (int)((double)currentRecords / (double)totalRecords * 100) / 2;
            }
            else
            {
                Utils.DebugTool.WriteLine("NormalMode");
                var listMiddleTempFIles = new List<CCTempFile>();
                foreach (var file in this._viewModel.InputFiles)
                {
                    var ccTempFile = new CCTempFile(this._viewModel.TempFiles.Add());
                    ccTempFile.CsvConfig = file.GetCsvConfig();
                    ccTempFile.RawHeader = file.RawHeader;
                    ccTempFile.originalFilePath = file.FilePath;
                    ccTempFiles.Add(ccTempFile);
                    listMiddleTempFIles.Add(ccTempFile);

                    var maxSortFileRecords = Global.Parameter.GetMaxSortFileRecords(file.Header.Length);
                    currentRecords += await SortCsvFileAsync(file.FilePath, ccTempFile.FilePath, ccTempFile.CsvConfig,comp, ccTempFile.RawHeader, maxSortFileRecords);
                    this._viewModel.ProgressValue = (int)((double)currentRecords / (double)totalRecords * 100) / 2;
                }

                foreach (var ccTempFile in listMiddleTempFIles)
                {
                    var outputFilePath = Path.Combine(this._viewModel.OutputFolder, Path.GetFileName(ccTempFile.originalFilePath));
                    currentRecords += await OutputCsvFileAsync(ccTempFile.FilePath, outputFilePath, ccTempFile.CsvConfig, splitInfo, ccTempFile.RawHeader);
                    this._viewModel.ProgressValue = (int)((double)currentRecords / (double)totalRecords * 100) / 2;
                }

            }

            if(totalRecords * 2 == currentRecords)
            {
                this._viewModel.ProgressValue = 100;
            }

            foreach (var ccTempFile in ccTempFiles)
            {
                this._viewModel.TempFiles.Delete(ccTempFile.Guid);
            }
            this._viewModel.StopStopwatch();
            this._viewModel.ChangeProcessingStatus(false);
            MessageBox.Show("変換が完了しました。","メッセージ",MessageBoxButton.OK,MessageBoxImage.Information);
        }

        private async Task<long> CountCsvFileAsync(string inputFile, CsvConfiguration config)
        {
            Utils.DebugTool.WriteLine("CountCsvFileAsync:" + inputFile);
            long count = 0;
            if (!this.DicCountCsvFile.ContainsKey(inputFile))
            {
                using (var reader = new StreamReader(inputFile, config.Encoding))
                {
                    using (var csv = new CsvReader(reader, config))
                    {
                        while (await csv.ReadAsync())
                        {
                            var data = csv.GetRecord<dynamic>() as IDictionary<string, object>;
                            count++;
                        }
                    }
                }
                this.DicCountCsvFile.Add(inputFile, count);
            }
            else
            {
                count = this.DicCountCsvFile[inputFile];
            }
            return count;
        }

        private async Task<long> SortCsvFileAsync(string inputFile, string outputFile, CsvConfiguration config, SortComparer comp, string rawHeader, long maxSortFileRecords)
        {
            Utils.DebugTool.WriteLine("SortCsvFileAsync:" + inputFile + " -> " + outputFile);

            long count = await CountCsvFileAsync(inputFile, config);

            long countRecords = 0;
            if(comp.isEmpty())
            {
                File.Copy(inputFile, outputFile, true);
                countRecords = count;
            }
            else if (count <= maxSortFileRecords)
            {
                var list = new List<Models.SortCsvRow>();
                using (var reader = new StreamReader(inputFile, config.Encoding))
                {
                    using (var csv = new CsvReader(reader, config))
                    {
                        while (await csv.ReadAsync())
                        {
                            var row = new Models.SortCsvRow();
                            row.Data = csv.GetRecord<dynamic>() as IDictionary<string, object>;
                            row.RawData = csv.Context.Parser.RawRecord;
                            if (!row.RawData.EndsWith(config.NewLine))
                            {
                                row.RawData = row.RawData + config.NewLine;
                            }
                            list.Add(row);
                        }
                    }
                }

                list.Sort(comp);

                using (var writer = new StreamWriter(outputFile, false, config.Encoding))
                {
                    await writer.WriteAsync(rawHeader + config.NewLine);
                    foreach (var data in list)
                    {
                        await writer.WriteAsync(data.RawData);
                        countRecords++;
                    }
                }
            }
            else
            {
                var tmpFiles = new List<CCTempFile>();
                var tmpFile = new CCTempFile(this._viewModel.TempFiles.Add());
                tmpFile.CsvConfig = config;
                tmpFile.RawHeader = rawHeader;
                tmpFiles.Add(tmpFile);

                int countTmp = 0;
                var list = new List<Models.SortCsvRow>();
                using (var reader = new StreamReader(inputFile, config.Encoding))
                {
                    using (var csv = new CsvReader(reader, config))
                    {
                        while (await csv.ReadAsync())
                        {
                            var row = new Models.SortCsvRow();
                            row.Data = csv.GetRecord<dynamic>() as IDictionary<string, object>;
                            row.RawData = csv.Context.Parser.RawRecord;
                            if (!row.RawData.EndsWith(config.NewLine))
                            {
                                row.RawData = row.RawData + config.NewLine;
                            }
                            list.Add(row);
                            countTmp++;
                            if (countTmp >= maxSortFileRecords)
                            {
                                list.Sort(comp);
                                using (var writer = new StreamWriter(tmpFile.FilePath, false, config.Encoding))
                                {
                                    await writer.WriteAsync(rawHeader + config.NewLine);
                                    int wkCount = 0;
                                    StringBuilder buff = new StringBuilder();
                                    foreach (var data in list)
                                    {
                                        wkCount++;
                                        buff.Append(data.RawData);
                                        if(wkCount >= Global.Const.WRITE_BUFFER_SIZE)
                                        {
                                            await writer.WriteAsync(buff.ToString());
                                            buff.Clear();
                                            wkCount = 0;
                                        }
                                        //await writer.WriteAsync(data.RawData);
                                    }
                                    if (wkCount > 0)
                                    {
                                        await writer.WriteAsync(buff.ToString());
                                        buff.Clear();
                                        wkCount = 0;
                                    }
                                }
                                list.Clear();
                                countTmp = 0;
                                tmpFile = new CCTempFile(this._viewModel.TempFiles.Add());
                                tmpFile.CsvConfig = config;
                                tmpFile.RawHeader = rawHeader;
                                tmpFiles.Add(tmpFile);
                            }
                        }
                    }
                    if(list.Count > 0)
                    {
                        list.Sort(comp);
                        using (var writer = new StreamWriter(tmpFile.FilePath, false, config.Encoding))
                        {
                            await writer.WriteAsync(rawHeader + config.NewLine);
                            int wkCount = 0;
                            StringBuilder buff = new StringBuilder();
                            foreach (var data in list)
                            {
                                wkCount++;
                                buff.Append(data.RawData);
                                if (wkCount >= Global.Const.WRITE_BUFFER_SIZE)
                                {
                                    await writer.WriteAsync(buff.ToString());
                                    buff.Clear();
                                    wkCount = 0;
                                }
                                //await writer.WriteAsync(data.RawData);
                            }
                            if (wkCount > 0)
                            {
                                await writer.WriteAsync(buff.ToString());
                                buff.Clear();
                                wkCount = 0;
                            }
                        }
                    }
                }

                var inputTmpFiles = new List<string>();
                foreach (var tmp in tmpFiles)
                {
                    inputTmpFiles.Add(tmp.FilePath);
                }
                countRecords = await MergeCsvFileAsync(inputTmpFiles, outputFile, config, comp, rawHeader);

                foreach (var tmp in tmpFiles)
                {
                    this._viewModel.TempFiles.Delete(tmp.Guid);
                }

            }

            return countRecords;
        }

        public async Task<long> MergeCsvFileAsync(List<string> inputFiles, string outputFile, CsvConfiguration config, SortComparer comp, string rawHeader)
        {
            Utils.DebugTool.WriteLine("MergeCsvFileAsync:" + string.Join(",", inputFiles) + " -> " + outputFile);
            var inputList = new List<CCInput>();
            foreach (var inputFile in inputFiles)
            {
                var ccInput = new CCInput(inputFile, config.Encoding, config);
                inputList.Add(ccInput);
            }

            foreach (var ccInput in inputList)
            {
                await ccInput.ReadAsync();
            }

            long countRecords = 0;
            using (var writer = new StreamWriter(outputFile, false, config.Encoding))
            {
                await writer.WriteAsync(rawHeader + config.NewLine);
                while (true)
                {
                    CCInput ccInputMin = null;
                    int count = 0;
                    for (int i = 0; i < inputList.Count; i++)
                    {
                        if (ccInputMin is null && !inputList[i].Closed)
                        {
                            ccInputMin = inputList[i];
                            count++;
                        }
                        else if (
                                   ccInputMin != null && !inputList[i].Closed
                                && !comp.isEmpty()
                            )
                        {
                            var tmp1 = new SortCsvRow();
                            tmp1.Data = ccInputMin.CurrentRecord;
                            tmp1.RawData = ccInputMin.RawRecord;
                            var tmp2 = new SortCsvRow();
                            tmp2.Data = inputList[i].CurrentRecord;
                            tmp2.RawData = inputList[i].RawRecord;
                            if (comp.Compare(tmp1, tmp2) > 0)
                            {
                                ccInputMin = inputList[i];
                                count++;
                            }
                        }
                    }
                    if (count == 0)
                    {
                        break;
                    }
                    var row = ccInputMin.RawRecord;
                    if (!row.EndsWith(config.NewLine))
                    {
                        row = row + config.NewLine;
                    }
                    await writer.WriteAsync(row);
                    await ccInputMin.ReadAsync();
                    countRecords++;
                }
            }

            foreach (var ccInput in inputList)
            {
                ccInput.Close();
            }

            return countRecords;
        }

        private async Task<long> OutputCsvFileAsync(string inputFile, string outputFile, CsvConfiguration config,CCSplitInfo ccSplitInfo, string rawHeader)
        {
            Utils.DebugTool.WriteLine("OutputCsvFileAsync:" + inputFile + " -> " + outputFile);
            List<CCHeaderData> cCHeaderDataList = new List<CCHeaderData>();
            using(var reader = new StreamReader(inputFile, config.Encoding))
            {
                using(var csv = new CsvReader(reader,config))
                {
                    while (await csv.ReadAsync())
                    {
                        var data = csv.GetRecord<dynamic>() as IDictionary<string, object>;
                        var headerData = new List<string>();
                        foreach(var val in ccSplitInfo.Headers)
                        {
                            headerData.Add(data[val].ToString());
                        }
                        AddCCHeaderData(ref cCHeaderDataList, headerData);
                    }
                }
            }

            var baseFileName = Path.GetFileNameWithoutExtension(outputFile);
            var extention = Path.GetExtension(outputFile);
            var outputFolder = Directory.GetParent(outputFile).FullName;

            var listOutput = new List<CCOutput>();
            foreach (var ccHeaderData in cCHeaderDataList)
            {
                var outputFilePath = GetOutputFilePath(baseFileName + ccHeaderData.GetJoinHeaderData(), extention, outputFolder, ref this.outputFiles);
                outputFiles.Add(outputFilePath);
                var ccOutput = new CCOutput(outputFilePath, config.Encoding);
                ccOutput.HeaderData = ccHeaderData.List;
                await ccOutput.WriteAsync(rawHeader + config.NewLine);
                listOutput.Add(ccOutput);
            }

            long countRecords = 0;
            using (var reader = new StreamReader(inputFile, config.Encoding))
            {
                using(var csv = new CsvReader(reader,config))
                {
                    while (await csv.ReadAsync())
                    {
                        var data = csv.GetRecord<dynamic>() as IDictionary<string, object>;
                        var row = csv.Context.Parser.RawRecord;
                        if(!row.EndsWith(config.NewLine))
                        {
                            row = row + config.NewLine;
                        }
                        var headerData = new List<string>();
                        foreach (var val in ccSplitInfo.Headers)
                        {
                            headerData.Add(data[val].ToString());
                        }
                        bool isOutput = false;
                        foreach (var ccOutput in listOutput)
                        {
                            var tmpCount = 0;
                            for (int i = 0; i < ccOutput.HeaderData.Count; i++)
                            {
                                if (ccOutput.HeaderData[i] == headerData[i])
                                {
                                    tmpCount++;
                                }
                            }
                            if (tmpCount == ccOutput.HeaderData.Count)
                            {
                                if(ccOutput.Counter >= this._viewModel.MaxCsvRecords)
                                {
                                    await ccOutput.WriteFlush();
                                    var outputFilePath = GetOutputFilePath(baseFileName + ccOutput.GetJoinHeaderData(), extention, outputFolder, ref this.outputFiles);
                                    outputFiles.Add(outputFilePath);
                                    ccOutput.Reset(outputFilePath);
                                    await ccOutput.WriteAsync(rawHeader + config.NewLine);
                                }
                                await ccOutput.WriteAsync(row);
                                isOutput = true;
                                countRecords++;
                                break;
                            }
                        }
                        if(!isOutput)
                        {
                            throw new Exception("Output Error: ");
                        }

                    }
                }
            }

            foreach (var ccOutput in listOutput)
            {
                await ccOutput.WriteFlush();
                ccOutput.Close();
            }

            return countRecords;
        }

        public string GetOutputFilePath(string baseFileName, string extention, string outputFolder,ref List<string> outputFiles)
        {
            var result = "";

            var invalidChars = Path.GetInvalidFileNameChars();
            int i = 1;
            while (true)
            {
                var outputFileName = baseFileName + "_" + i.ToString() + extention;
                foreach (var invalidChar in invalidChars)
                {
                    outputFileName = outputFileName.Replace(invalidChar, '_');
                }
                outputFileName = outputFileName.Replace(" ", "_");
                outputFileName = outputFileName.Replace("　", "_");
                var outputFilePath = Path.Combine(outputFolder, outputFileName);
                if (!outputFiles.Contains(outputFilePath))
                {
                    result = outputFilePath;
                    break;
                }
                i++;
            }

            return result;
        }

        public void AddCCHeaderData(ref List<CCHeaderData> cCHeaderDataList, List<string> headers)
        {
            var cCHeaderData = new CCHeaderData();
            cCHeaderData.Add(headers);
            for (int i = 0; i < cCHeaderDataList.Count; i++)
            {
                int tmpCount = 0;
                for (int j = 0; j < cCHeaderDataList[i].List.Count; j++)
                {
                    if (cCHeaderDataList[i].List[j] == cCHeaderData.List[j])
                    {
                        tmpCount++;
                    }
                }
                if (tmpCount == cCHeaderDataList[i].List.Count)
                {
                    return;
                }
            }
            cCHeaderDataList.Add(cCHeaderData);
        }
    }

    public class CCTempFile
    {
        public CCTempFile() { }
        public CCTempFile(Models.TempFile tmpFile)
        {
            this.FilePath = tmpFile.FilePath;
            this.Guid = tmpFile.Guid;
        }
        public string FilePath { get; set; }
        public Guid Guid { get; set; }
        public CsvConfiguration CsvConfig { get; set; }
        public string RawHeader { get; set; }
        public string originalFilePath { get; set; }
    }

    public class CCSplitInfo
    {
        public List<string> Headers;
        public CCSplitInfo()
        {
            this.Headers = new List<string>();
        }

        public void AddHeader(string header)
        {
            if (!string.IsNullOrEmpty(header))
            {
                this.Headers.Add(header);
            }
        }
    }

    public class CCHeaderData
    {
        public List<string> List;
        public CCHeaderData()
        {
            this.List = new List<string>();
        }

        public void Add(List<string> data)
        {
            List = data;
        }

        public string GetJoinHeaderData()
        {
            var tmp = "";
            if(this.List.Count != 0)
            {
                tmp = "_" + string.Join("_", List).Replace(" ", "").Replace("　", "");
            }
            return tmp;
        }
    }

    public class CCOutput
    {
        private StreamWriter _writer;
        private Encoding _encoding;
        private string _filePath;
        private StringBuilder _buffer;
        private int _bufferCount;
        public string FilePath
        {
            get
            {
                return _filePath;
            }
        }
        public List<string> HeaderData { get; set; }
        public CCOutput(string prmFilePath, Encoding prmEncoding)
        {
            this._filePath = prmFilePath;
            this._encoding = prmEncoding;
            this._writer = new StreamWriter(prmFilePath, false, this._encoding);
            this._buffer = new StringBuilder();
            this._bufferCount = 0;
        }
        private bool _closed = false;
        public bool Closed
        {
            get
            {
                return _closed;
            }
        }
        private long _counter = 0;
        public long Counter
        {
            get
            {
                return _counter;
            }
        }

        public void Close()
        {
            if (!this._closed)
            {
                if(this._bufferCount > 0)
                {
                    this._writer.Write(this._buffer.ToString());
                    this._buffer.Clear();
                    this._bufferCount = 0;
                }
                this._writer.Close();
                this._closed = true;
            }
        }

        public void Reset(string prmFilePath)
        {
            if(!this._closed)
            {
                this.Close();
            }
            this._filePath = prmFilePath;
            this._writer = new StreamWriter(prmFilePath, false, this._encoding);
            this._counter = 0;
            this._closed = false;
        }

        public async Task WriteAsync(string data)
        {
            this._buffer.Append(data);
            this._bufferCount++;
            if (this._bufferCount >= Global.Const.WRITE_BUFFER_SIZE)
            {
                await this._writer.WriteAsync(this._buffer.ToString());
                this._buffer.Clear();
                this._bufferCount = 0;
            }
            //await this._writer.WriteAsync(data);
            this._counter++;
        }
        public async Task WriteFlush()
        {
            if (this._bufferCount > 0)
            {
                await this._writer.WriteAsync(this._buffer.ToString());
                this._buffer.Clear();
                this._bufferCount = 0;
            }
        }

        public string GetJoinHeaderData()
        {
            var tmp = "";
            if (this.HeaderData.Count != 0)
            {
                tmp = "_" + string.Join("_", HeaderData).Replace(" ","").Replace("　", "");
            }
            return tmp;
        }
    }

    public class CCInput
    {
        private StreamReader _reader;
        private CsvReader _csvReader { get; set; }
        private CsvConfiguration _csvConfig { get; set; }
        private Encoding _encoding;
        private string _filePath;
        private IDictionary<string, object> _currentRecord;
        private string _rawRecord;
        public IDictionary<string, object> CurrentRecord
        {
            get
            {
                return this._currentRecord;
            }
        }
        public string RawRecord
        {
            get
            {
                return this._rawRecord;
            }
        }
        public string FilePath
        {
            get
            {
                return _filePath;
            }
        }
        public CsvConfiguration CsvConfig
        {
            get
            {
                return _csvConfig;
            }
        }
        public CCInput(string prmFilePath, Encoding prmEncoding, CsvConfiguration csvConfig)
        {
            this._filePath = prmFilePath;
            this._encoding = prmEncoding;
            this._csvConfig = csvConfig;
            this._reader = new StreamReader(prmFilePath, this._encoding);
            this._csvReader = new CsvReader(this._reader,this._csvConfig);
        }
        private bool _closed = false;
        public bool Closed
        {
            get
            {
                return _closed;
            }
        }
        private long _counter = 0;
        public long Counter
        {
            get
            {
                return _counter;
            }
        }

        public void Close()
        {
            if (!this._closed)
            {
                this._reader.Close();
                this._closed = true;
            }
        }

        public async Task ReadAsync()
        {
            bool rtn = await this._csvReader.ReadAsync();
            if (rtn)
            {
                this._currentRecord = this._csvReader.GetRecord<dynamic>() as IDictionary<string, object>;
                this._rawRecord = this._csvReader.Context.Parser.RawRecord;
                this._counter++;
            }
            else
            {
                this._currentRecord = null;
                this._rawRecord = null;
                Close();
            }
        }
    }
}
