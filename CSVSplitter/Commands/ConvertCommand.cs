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
using CSVSplitter.ViewModels;

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
        private HashSet<string> outputFilePathSet;
        private Dictionary<string, long> DicCountCsvFile;
        private Dictionary<string, long> DicEstimatedCsvFile;
        private HashSet<string> ReconciledFiles;
        private UpdateProgressStatus updateProgressStatus { get; set; }
        private bool _updateProgressModeAtMerge = true;

        public async void Execute(object parameter)
        {
            Utils.DebugTool.WriteLine("Execute Convert");
            try
            {
                updateProgressStatus = new UpdateProgressStatus(this._viewModel);
                this._viewModel.ChangeProcessingStatus(true);
                this._viewModel.ProgressValue = 0;
                this._viewModel.ResetStopwatch();
                this._viewModel.StartStopwatch();

                var listSortOptions = new List<Models.SortOption>();
                if (!string.IsNullOrEmpty(this._viewModel.SortItem1SelectedValue))
                {
                    listSortOptions.Add(new Models.SortOption(this._viewModel.SortItem1SelectedValue, this._viewModel.SortItem1IsDescending, this._viewModel.SortItem1IsNumeric));
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

                if (!System.IO.Directory.Exists(this._viewModel.OutputFolder))
                {
                    System.IO.Directory.CreateDirectory(this._viewModel.OutputFolder);
                }

                this.outputFiles = new List<string>();
                this.outputFilePathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                this.DicCountCsvFile = new Dictionary<string, long>();
                this.DicEstimatedCsvFile = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                this.ReconciledFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                long totalRecords = 0;
                foreach (var file in this._viewModel.InputFiles)
                {
                    var estimated = await EstimateCsvFileRecordsAsync(file.FilePath, file.GetCsvConfig());
                    this.DicEstimatedCsvFile[file.FilePath] = estimated;
                    totalRecords += estimated;
                }
                long currentRecords = 0;
                updateProgressStatus.SetTotalAmount(totalRecords * 2);

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

                    _updateProgressModeAtMerge = false; // 統合モードでは、マージ時の進捗更新を無効にする
                    await MergeCsvFileAsync(this._viewModel.InputFiles.Select(f => f.FilePath).ToList(), ccTempFile.FilePath, ccTempFile.CsvConfig, comp, ccTempFile.RawHeader);
                    _updateProgressModeAtMerge = true; // マージ後は進捗更新を有効に戻す
                    var ccTempFile2 = new CCTempFile(this._viewModel.TempFiles.Add());
                    ccTempFile2.CsvConfig = ccTempFile.CsvConfig;
                    ccTempFile2.RawHeader = ccTempFile.RawHeader;
                    ccTempFile2.originalFilePath = ccTempFile.originalFilePath;
                    ccTempFiles.Add(ccTempFile2);
                    listMiddleTempFIles.Add(ccTempFile2);

                    var maxSortFileRecords = Global.Parameter.GetMaxSortFileRecords(this._viewModel.InputFiles[0].Header.Length);
                    var sortedCount = await SortCsvFileAsync(ccTempFile.FilePath, ccTempFile2.FilePath, ccTempFile.CsvConfig, comp, ccTempFile.RawHeader, maxSortFileRecords);
                    currentRecords += sortedCount;
                    totalRecords = ReconcileTotalRecords(totalRecords, sortedCount, this._viewModel.InputFiles.Select(f => f.FilePath));
                    updateProgressStatus.SetTotalAmount(totalRecords * 2);
                    //this._viewModel.ProgressValue = (int)((double)currentRecords / (double)totalRecords * 100) / 2;

                    var outputFilePath = Path.Combine(this._viewModel.OutputFolder, Path.GetFileName(ccTempFile2.originalFilePath));
                    currentRecords += await OutputCsvFileAsync(ccTempFile2.FilePath, outputFilePath, ccTempFile2.CsvConfig, splitInfo, ccTempFile2.RawHeader);
                    //this._viewModel.ProgressValue = (int)((double)currentRecords / (double)totalRecords * 100) / 2;
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
                        var sortedCount = await SortCsvFileAsync(file.FilePath, ccTempFile.FilePath, ccTempFile.CsvConfig, comp, ccTempFile.RawHeader, maxSortFileRecords);
                        currentRecords += sortedCount;
                        totalRecords = ReconcileTotalRecords(totalRecords, sortedCount, new[] { file.FilePath });
                        updateProgressStatus.SetTotalAmount(totalRecords * 2);
                        this._viewModel.ProgressValue = (int)((double)currentRecords / (double)totalRecords * 100) / 2;
                    }

                    foreach (var ccTempFile in listMiddleTempFIles)
                    {
                        var outputFilePath = Path.Combine(this._viewModel.OutputFolder, Path.GetFileName(ccTempFile.originalFilePath));
                        currentRecords += await OutputCsvFileAsync(ccTempFile.FilePath, outputFilePath, ccTempFile.CsvConfig, splitInfo, ccTempFile.RawHeader);
                        this._viewModel.ProgressValue = (int)((double)currentRecords / (double)totalRecords * 100) / 2;
                    }

                }

                // 処理が完了しているので、進捗を100%に設定
                this._viewModel.ProgressValue = 100;

                foreach (var ccTempFile in ccTempFiles)
                {
                    this._viewModel.TempFiles.Delete(ccTempFile.Guid);
                }
                this._viewModel.StopStopwatch();
                this._viewModel.ChangeProcessingStatus(false);
                MessageBox.Show("変換が完了しました。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception e)
            {
                MessageBox.Show("変換中にエラーが発生しました。: " + e.ToString(), "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Utils.DebugTool.WriteLine("ConvertCommand.Execute Error: " + e.ToString());
                throw e;
            }
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
                        if (config.HasHeaderRecord)
                        {
                            if (await csv.ReadAsync())
                            {
                                csv.ReadHeader();
                            }
                        }

                        while (await csv.ReadAsync())
                        {
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

        private async Task<long> EstimateCsvFileRecordsAsync(string inputFile, CsvConfiguration config)
        {
            const int bufferSize = 1024 * 64;
            byte[] buffer = new byte[bufferSize];
            long newLineCount = 0;
            long bytesReadTotal = 0;
            byte lastByte = 0;
            bool hasLastByte = false;

            using (var stream = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                while (true)
                {
                    int readSize = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (readSize <= 0)
                    {
                        break;
                    }
                    bytesReadTotal += readSize;
                    for (int i = 0; i < readSize; i++)
                    {
                        if (buffer[i] == (byte)'\n')
                        {
                            newLineCount++;
                        }
                    }
                    lastByte = buffer[readSize - 1];
                    hasLastByte = true;
                }
            }

            if (bytesReadTotal == 0)
            {
                return 0;
            }

            long estimatedLines = newLineCount;
            if (hasLastByte && lastByte != (byte)'\n' && lastByte != (byte)'\r')
            {
                estimatedLines++;
            }

            if (config.HasHeaderRecord && estimatedLines > 0)
            {
                estimatedLines--;
            }

            return Math.Max(0, estimatedLines);
        }

        private long ReconcileTotalRecords(long estimatedTotalRecords, long actualRecords, IEnumerable<string> files)
        {
            long estimatedPart = 0;
            bool hasTarget = false;
            foreach (var file in files)
            {
                if (this.ReconciledFiles.Contains(file))
                {
                    continue;
                }

                if (this.DicEstimatedCsvFile.TryGetValue(file, out var estimated))
                {
                    estimatedPart += estimated;
                }
                this.ReconciledFiles.Add(file);
                hasTarget = true;
            }

            if (!hasTarget)
            {
                return estimatedTotalRecords;
            }

            long correctedTotal = estimatedTotalRecords - estimatedPart + actualRecords;
            return Math.Max(correctedTotal, 1);
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
                this.updateProgressStatus.IncrementCount(countRecords);
            }
            else if (count <= maxSortFileRecords)
            {
                var list = new List<Models.SortCsvRow>();
                var sortHeaders = comp.Options.Select(o => o.ColName).ToArray();
                using (var reader = new StreamReader(inputFile, config.Encoding))
                {
                    using (var csv = new CsvReader(reader, config))
                    {
                        int[] sortHeaderIndexes = null;
                        if (config.HasHeaderRecord)
                        {
                            if (await csv.ReadAsync())
                            {
                                csv.ReadHeader();
                                sortHeaderIndexes = ResolveHeaderIndexes(csv.HeaderRecord, sortHeaders);
                            }
                        }

                        while (await csv.ReadAsync())
                        {
                            var row = new Models.SortCsvRow();
                            var recordValues = csv.Parser.Record ?? Array.Empty<string>();
                            row.RawData = csv.Context.Parser.RawRecord;
                            if (!row.RawData.EndsWith(config.NewLine))
                            {
                                row.RawData = row.RawData + config.NewLine;
                            }
                            row.SetSortKey(comp, recordValues, sortHeaderIndexes ?? Array.Empty<int>());
                            list.Add(row);
                        }
                    }
                }

                list = SortRows(list, comp);

                using (var writer = new StreamWriter(outputFile, false, config.Encoding))
                {
                    await writer.WriteAsync(rawHeader + config.NewLine);
                    foreach (var data in list)
                    {
                        await writer.WriteAsync(data.RawData);
                        countRecords++;
                        this.updateProgressStatus.IncrementCount(1);
                    }
                }
            }
            else
            {
                var tmpFiles = new List<CCTempFile>(10000);
                var tmpFile = new CCTempFile(this._viewModel.TempFiles.Add());
                tmpFile.CsvConfig = config;
                tmpFile.RawHeader = rawHeader;
                tmpFiles.Add(tmpFile);

                int countTmp = 0;
                var list = new List<Models.SortCsvRow>();
                var sortHeaders = comp.Options.Select(o => o.ColName).ToArray();
                using (var reader = new StreamReader(inputFile, config.Encoding))
                {
                    using (var csv = new CsvReader(reader, config))
                    {
                        int[] sortHeaderIndexes = null;
                        if (config.HasHeaderRecord)
                        {
                            if (await csv.ReadAsync())
                            {
                                csv.ReadHeader();
                                sortHeaderIndexes = ResolveHeaderIndexes(csv.HeaderRecord, sortHeaders);
                            }
                        }

                        StringBuilder buff = new StringBuilder(10000);
                        while (await csv.ReadAsync())
                        {
                            var row = new Models.SortCsvRow();
                            var recordValues = csv.Parser.Record ?? Array.Empty<string>();
                            row.RawData = csv.Context.Parser.RawRecord;
                            if (!row.RawData.EndsWith(config.NewLine))
                            {
                                row.RawData = row.RawData + config.NewLine;
                            }
                            row.SetSortKey(comp, recordValues, sortHeaderIndexes ?? Array.Empty<int>());
                            list.Add(row);
                            countTmp++;
                            if (countTmp >= maxSortFileRecords)
                            {
                                list = SortRows(list, comp);
                                using (var writer = new StreamWriter(tmpFile.FilePath, false, config.Encoding))
                                {
                                    await writer.WriteAsync(rawHeader + config.NewLine);
                                    int wkCount = 0;
                                    buff.Clear();
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
                        list = SortRows(list, comp);
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
            var inputList = new List<CCInput>(inputFiles.Count);
            foreach (var inputFile in inputFiles)
            {
                var ccInput = new CCInput(inputFile, config.Encoding, config);
                inputList.Add(ccInput);
            }

            foreach (var ccInput in inputList)
            {
                await ccInput.ReadAsync();
                ccInput.SetSortKeys(comp);
            }

            long countRecords = 0;
            using (var writer = new StreamWriter(outputFile, false, config.Encoding))
            {
                await writer.WriteAsync(rawHeader + config.NewLine);
                if (comp.isEmpty())
                {
                    foreach (var input in inputList)
                    {
                        while (!input.Closed)
                        {
                            var row = input.RawRecord;
                            if (!row.EndsWith(config.NewLine))
                            {
                                row = row + config.NewLine;
                            }
                            await writer.WriteAsync(row);
                            await input.ReadAsync();
                            input.SetSortKeys(comp);
                            countRecords++;
                            if (_updateProgressModeAtMerge)
                            {
                                this.updateProgressStatus.IncrementCount(1);
                            }
                        }
                    }
                }
                else
                {
                    var heap = new PriorityQueue<CCInput, CCInput>(new CCInputPriorityComparer(comp));
                    foreach (var input in inputList)
                    {
                        if (!input.Closed)
                        {
                            heap.Enqueue(input, input);
                        }
                    }

                    while (heap.TryDequeue(out var ccInputMin, out _))
                    {
                        var row = ccInputMin.RawRecord;
                        if (!row.EndsWith(config.NewLine))
                        {
                            row = row + config.NewLine;
                        }
                        await writer.WriteAsync(row);
                        await ccInputMin.ReadAsync();
                        ccInputMin.SetSortKeys(comp);
                        countRecords++;
                        if (_updateProgressModeAtMerge)
                        {
                            this.updateProgressStatus.IncrementCount(1);
                        }

                        if (!ccInputMin.Closed)
                        {
                            heap.Enqueue(ccInputMin, ccInputMin);
                        }
                    }
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

            var baseFileName = Path.GetFileNameWithoutExtension(outputFile);
            var extention = Path.GetExtension(outputFile);
            var outputFolder = Directory.GetParent(outputFile).FullName;
            var outputByHeader = new Dictionary<SplitRoutingKey, CCOutput>(SplitRoutingKeyComparer.Instance);
            var splitHeaderCount = ccSplitInfo.Headers.Count;
            var splitHeaders = ccSplitInfo.Headers.ToArray();

            long countRecords = 0;
            using (var reader = new StreamReader(inputFile, config.Encoding))
            {
                using(var csv = new CsvReader(reader,config))
                {
                    int[] splitHeaderIndexes = null;
                    if (config.HasHeaderRecord)
                    {
                        if (await csv.ReadAsync())
                        {
                            csv.ReadHeader();
                            splitHeaderIndexes = ResolveSplitHeaderIndexes(csv.HeaderRecord, splitHeaders);
                        }
                    }
                    else
                    {
                        splitHeaderIndexes = ResolveSplitHeaderIndexes(csv.HeaderRecord, splitHeaders);
                    }

                    while (await csv.ReadAsync())
                    {
                        var row = csv.Context.Parser.RawRecord;
                        if(!row.EndsWith(config.NewLine))
                        {
                            row = row + config.NewLine;
                        }
                        var record = csv.Parser.Record ?? Array.Empty<string>();
                        var headerData = new string[splitHeaderCount];
                        for (int i = 0; i < splitHeaderCount; i++)
                        {
                            var index = splitHeaderIndexes[i];
                            headerData[i] = (index >= 0 && index < record.Length) ? record[index] ?? string.Empty : string.Empty;
                        }
                        var headerKey = new SplitRoutingKey(headerData);
                        if (!outputByHeader.TryGetValue(headerKey, out var ccOutput))
                        {
                            // Create new output file
                            var ccHeaderData = new CCHeaderData();
                            ccHeaderData.Set(headerData.ToList());

                            var outputFilePath = GetOutputFilePath(baseFileName + ccHeaderData.GetJoinHeaderData(), extention, outputFolder);
                            outputFiles.Add(outputFilePath);
                            ccOutput = new CCOutput(outputFilePath, config.Encoding);
                            ccOutput.HeaderData = ccHeaderData.List;
                            await ccOutput.WriteAsync(rawHeader + config.NewLine);
                            outputByHeader.Add(headerKey, ccOutput);
                        }
                        
                        if (ccOutput.Counter >= this._viewModel.MaxCsvRecords)
                        {
                            await ccOutput.WriteFlush();
                            var outputFilePath = GetOutputFilePath(baseFileName + ccOutput.GetJoinHeaderData(), extention, outputFolder);
                            outputFiles.Add(outputFilePath);
                            ccOutput.Reset(outputFilePath);
                            await ccOutput.WriteAsync(rawHeader + config.NewLine);
                        }
                        await ccOutput.WriteAsync(row);
                        countRecords++;
                        this.updateProgressStatus.IncrementCount(1);

                    }
                }
            }

            foreach (var ccOutput in outputByHeader.Values)
            {
                await ccOutput.WriteFlush();
                ccOutput.Close();
            }

            return countRecords;
        }

        public string GetOutputFilePath(string baseFileName, string extention, string outputFolder)
        {
            EnsureOutputPathSetInitialized();

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
                if (this.outputFilePathSet.Add(outputFilePath))
                {
                    result = outputFilePath;
                    break;
                }
                i++;
            }

            return result;
        }

        public string GetOutputFilePath(string baseFileName, string extention, string outputFolder, ref List<string> outputFiles)
        {
            EnsureOutputPathSetInitialized(outputFiles);
            return GetOutputFilePath(baseFileName, extention, outputFolder);
        }

        private int[] ResolveSplitHeaderIndexes(string[] headerRecord, string[] splitHeaders)
        {
            return ResolveHeaderIndexes(headerRecord, splitHeaders);
        }

        private int[] ResolveHeaderIndexes(string[] headerRecord, string[] targetHeaders)
        {
            if (targetHeaders == null || targetHeaders.Length == 0)
            {
                return Array.Empty<int>();
            }

            var headerMap = new Dictionary<string, int>(StringComparer.Ordinal);
            if (headerRecord != null)
            {
                for (int i = 0; i < headerRecord.Length; i++)
                {
                    var key = headerRecord[i] ?? string.Empty;
                    headerMap.TryAdd(key, i);
                }
            }

            var indexes = new int[targetHeaders.Length];
            for (int i = 0; i < targetHeaders.Length; i++)
            {
                var key = targetHeaders[i] ?? string.Empty;
                if (!headerMap.TryGetValue(key, out indexes[i]))
                {
                    indexes[i] = -1;
                }
            }
            return indexes;
        }

        private IDictionary<string, string> ReadCurrentRecordAsDictionary(CsvReader csv)
        {
            var header = csv.HeaderRecord ?? Array.Empty<string>();
            var record = csv.Parser.Record ?? Array.Empty<string>();
            var data = new Dictionary<string, string>(header.Length, StringComparer.Ordinal);
            for (int i = 0; i < header.Length; i++)
            {
                var key = header[i] ?? string.Empty;
                var value = i < record.Length ? record[i] : null;
                data[key] = value;
            }
            return data;
        }

        private void EnsureOutputPathSetInitialized(List<string> seedOutputFiles = null)
        {
            if (this.outputFiles is null)
            {
                this.outputFiles = seedOutputFiles ?? new List<string>();
            }

            if (this.outputFilePathSet is null)
            {
                this.outputFilePathSet = new HashSet<string>(this.outputFiles, StringComparer.OrdinalIgnoreCase);
            }
        }

        public void AddCCHeaderData(ref HashSet<CCHeaderData> ccHeaderDataHashSet, List<string> headers)
        {
            var ccHeaderData = new CCHeaderData();
            ccHeaderData.Set(headers);
            ccHeaderDataHashSet.Add(ccHeaderData);
        }

        private List<SortCsvRow> SortRows(List<SortCsvRow> rows, SortComparer comparer)
        {
            if (rows == null || rows.Count <= 1)
            {
                return rows;
            }

            var parallelSortThreshold = ResolveParallelSortThreshold();
            if (rows.Count < parallelSortThreshold)
            {
                rows.Sort(comparer);
                return rows;
            }

            return rows.AsParallel()
                       .OrderBy(r => r, comparer)
                       .ToList();
        }

        private static int ResolveParallelSortThreshold()
        {
            var coreCount = Math.Max(Environment.ProcessorCount, 1);
            if (coreCount <= 1)
            {
                return int.MaxValue;
            }

            var autoThreshold = coreCount * Global.Const.PARALLEL_SORT_THRESHOLD_PER_CORE;
            if (autoThreshold < Global.Const.PARALLEL_SORT_THRESHOLD_MIN)
            {
                return Global.Const.PARALLEL_SORT_THRESHOLD_MIN;
            }
            if (autoThreshold > Global.Const.PARALLEL_SORT_THRESHOLD_MAX)
            {
                return Global.Const.PARALLEL_SORT_THRESHOLD_MAX;
            }
            return autoThreshold;
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

    internal readonly struct SplitRoutingKey
    {
        public readonly string[] Values;
        public readonly int HashCode;

        public SplitRoutingKey(string[] values)
        {
            Values = values ?? Array.Empty<string>();
            unchecked
            {
                var hash = 17;
                for (int i = 0; i < Values.Length; i++)
                {
                    hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Values[i] ?? string.Empty);
                }
                HashCode = hash;
            }
        }
    }

    internal sealed class SplitRoutingKeyComparer : IEqualityComparer<SplitRoutingKey>
    {
        public static readonly SplitRoutingKeyComparer Instance = new SplitRoutingKeyComparer();
        private SplitRoutingKeyComparer() { }

        public bool Equals(SplitRoutingKey x, SplitRoutingKey y)
        {
            if (x.Values.Length != y.Values.Length)
            {
                return false;
            }
            for (int i = 0; i < x.Values.Length; i++)
            {
                if (!StringComparer.Ordinal.Equals(x.Values[i] ?? string.Empty, y.Values[i] ?? string.Empty))
                {
                    return false;
                }
            }
            return true;
        }

        public int GetHashCode(SplitRoutingKey obj)
        {
            return obj.HashCode;
        }
    }

    internal class CCInputPriorityComparer : IComparer<CCInput>
    {
        private readonly SortComparer _comparer;

        public CCInputPriorityComparer(SortComparer comparer)
        {
            this._comparer = comparer;
        }

        public int Compare(CCInput x, CCInput y)
        {
            return this._comparer.CompareRecords(
                x.CurrentRecord,
                x.CurrentSortKeys,
                x.IsSortKeySet,
                y.CurrentRecord,
                y.CurrentSortKeys,
                y.IsSortKeySet);
        }
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

        public void Set(List<string> data)
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

        public override int GetHashCode()
        {
            int wkHash = 0;
            foreach (var val in this.List)
            {
                wkHash ^= val.GetHashCode();
            }
            return wkHash;
        }

        public override bool Equals(object obj)
        {
            if (obj is CCHeaderData other)
            {
                if (this.List.Count != other.List.Count)
                {
                    return false;
                }
                for (int i = 0; i < this.List.Count; i++)
                {
                    if (this.List[i] != other.List[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
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
        private IDictionary<string, string> _currentRecord;
        private string[] _currentRecordValues;
        private string[] _currentHeader;
        private SortKey[] _currentSortKeys;
        private bool _isSortKeySet;
        private bool _isHeaderInitialized;
        private Dictionary<string, int> _headerIndexes;
        private int[] _sortOptionIndexes;
        private SortComparer _sortOptionIndexSource;
        private string _rawRecord;
        public IDictionary<string, string> CurrentRecord
        {
            get
            {
                if (this._currentRecord == null && this._currentRecordValues != null)
                {
                    this._currentRecord = ReadCurrentRecordAsDictionary();
                }
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
        public SortKey[] CurrentSortKeys
        {
            get
            {
                return this._currentSortKeys;
            }
        }
        public bool IsSortKeySet
        {
            get
            {
                return this._isSortKeySet;
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
            this._isHeaderInitialized = false;
            this._headerIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
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
            if (!_isHeaderInitialized && this._csvConfig.HasHeaderRecord)
            {
                if (await this._csvReader.ReadAsync())
                {
                    this._csvReader.ReadHeader();
                    this._currentHeader = this._csvReader.HeaderRecord ?? Array.Empty<string>();
                }
                _isHeaderInitialized = true;
            }

            bool rtn = await this._csvReader.ReadAsync();
            if (rtn)
            {
                this._currentRecordValues = this._csvReader.Parser.Record;
                this._currentRecord = null;
                this._rawRecord = this._csvReader.Context.Parser.RawRecord;
                this._currentSortKeys = null;
                this._isSortKeySet = false;
                this._counter++;
            }
            else
            {
                this._currentRecord = null;
                this._currentRecordValues = null;
                this._rawRecord = null;
                this._currentSortKeys = null;
                this._isSortKeySet = false;
                Close();
            }
        }

        public void SetSortKeys(SortComparer comparer)
        {
            if (comparer is null || comparer.isEmpty() || this._currentRecordValues is null)
            {
                this._currentSortKeys = null;
                this._isSortKeySet = false;
                if (comparer is null || comparer.isEmpty())
                {
                    this._sortOptionIndexes = null;
                    this._sortOptionIndexSource = null;
                }
                return;
            }

            if (this._sortOptionIndexes == null || !object.ReferenceEquals(this._sortOptionIndexSource, comparer))
            {
                this._sortOptionIndexes = ResolveSortOptionIndexes(comparer);
                this._sortOptionIndexSource = comparer;
            }
            this._currentSortKeys = comparer.BuildSortKeys(this._currentRecordValues, this._sortOptionIndexes);
            this._isSortKeySet = true;
        }

        private int[] ResolveSortOptionIndexes(SortComparer comparer)
        {
            if (this._headerIndexes.Count == 0)
            {
                var header = this._currentHeader ?? this._csvReader.HeaderRecord ?? Array.Empty<string>();
                for (int i = 0; i < header.Length; i++)
                {
                    var key = header[i] ?? string.Empty;
                    this._headerIndexes.TryAdd(key, i);
                }
            }

            var indexes = new int[comparer.Options.Count];
            for (int i = 0; i < comparer.Options.Count; i++)
            {
                var colName = comparer.Options[i].ColName ?? string.Empty;
                indexes[i] = this._headerIndexes.TryGetValue(colName, out var index) ? index : -1;
            }
            return indexes;
        }

        private IDictionary<string, string> ReadCurrentRecordAsDictionary()
        {
            var header = this._currentHeader ?? Array.Empty<string>();
            var record = this._currentRecordValues ?? Array.Empty<string>();
            var data = new Dictionary<string, string>(header.Length, StringComparer.Ordinal);
            for (int i = 0; i < header.Length; i++)
            {
                var key = header[i] ?? string.Empty;
                var value = i < record.Length ? record[i] : null;
                data[key] = value;
            }
            return data;
        }
    }

    public class UpdateProgressStatus
    {
        private ViewModels.MainWindowViewModel _viewModel;
        private long _totalAmount;
        private long _updateTiming;
        private long _previousUpdateCount = 0;
        private long _count;
        public UpdateProgressStatus(ViewModels.MainWindowViewModel viewModel)
        {
            this._viewModel = viewModel;
            this._totalAmount = 0;
            this._updateTiming = 1;
            this._count = 0;
        }

        public void SetTotalAmount(long totalAmount)
        {
            this._totalAmount = totalAmount;
            this._updateTiming = totalAmount / 100; // 進捗を100分割
            this._previousUpdateCount = 0;
            UpdateProgress();
        }
        public void SetCount(long count)
        {
            this._count = count;
            UpdateProgress();
        }
        public void IncrementCount(long increment = 1)
        {
            this._count += increment;
            UpdateProgress();
        }
        public void ResetViewProgress()
        {
            this._viewModel.ProgressValue = (int)((double)this._count / (double)this._totalAmount * 100);
        }
        public void UpdateProgress()
        {
            if (this._totalAmount > 0 && this._updateTiming > 0)
            {
                if (this._count - this._previousUpdateCount >= this._updateTiming)
                {
                    this._viewModel.ProgressValue = (int)((double)this._count / (double)this._totalAmount * 100);
                    this._previousUpdateCount = this._count;
                }
            }
        }
    }
}
