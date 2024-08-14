using CSVSplitter.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CSVSplitter.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public Models.InputFiles InputFiles { get; set; }
        public Models.SortItems SortItems { get; set; }
        public Models.SplitItems SplitItems { get; set; }
        public Models.TempFiles TempFiles { get; set; }
        public SetInputFilesCommand SetInputFilesCommand { get; set; }
        public ResetCommand ResetCommand { get; set; }
        public AnalyzeInputFilesCommand AnalyzeInputFilesCommand { get; set; }
        public ConvertCommand ConvertCommand { get; set; }
        public OpenFolderCommand OpenFolderCommand { get; set; }
        public SelectFolderCommand SelectFolderCommand { get; set; }

        private bool _isProcessing;

        private string _analysisReportOfInputFiles;

        private bool _integrationMode;

        private string _outputFolder;

        private string _sortItem1SelectedValue;
        private string _sortItem2SelectedValue;
        private string _sortItem3SelectedValue;
        private string _sortItem4SelectedValue;
        public string SortItem1SelectedValue
        {
            get
            {
                return _sortItem1SelectedValue;
            }
            set
            {
                _sortItem1SelectedValue = value;
                RaisePropertyChanged();
            }
        }
        public string SortItem2SelectedValue
        {
            get
            {
                return _sortItem2SelectedValue;
            }
            set
            {
                _sortItem2SelectedValue = value;
                RaisePropertyChanged();
            }
        }
        public string SortItem3SelectedValue
        {
            get
            {
                return _sortItem3SelectedValue;
            }
            set
            {
                _sortItem3SelectedValue = value;
                RaisePropertyChanged();
            }
        }
        public string SortItem4SelectedValue
        {
            get
            {
                return _sortItem4SelectedValue;
            }
            set
            {
                _sortItem4SelectedValue = value;
                RaisePropertyChanged();
            }
        }

        private bool _sortItem1IsDescending;
        private bool _sortItem2IsDescending;
        private bool _sortItem3IsDescending;
        private bool _sortItem4IsDescending;
        public bool SortItem1IsDescending
        {
            get
            {
                return _sortItem1IsDescending;
            }
            set
            {
                _sortItem1IsDescending = value;
                RaisePropertyChanged();
            }
        }
        public bool SortItem2IsDescending
        {
            get
            {
                return _sortItem2IsDescending;
            }
            set
            {
                _sortItem2IsDescending = value;
                RaisePropertyChanged();
            }
        }
        public bool SortItem3IsDescending
        {
            get
            {
                return _sortItem3IsDescending;
            }
            set
            {
                _sortItem3IsDescending = value;
                RaisePropertyChanged();
            }
        }
        public bool SortItem4IsDescending
        {
            get
            {
                return _sortItem4IsDescending;
            }
            set
            {
                _sortItem4IsDescending = value;
                RaisePropertyChanged();
            }
        }

        private bool _sortItem1IsNumeric;
        private bool _sortItem2IsNumeric;
        private bool _sortItem3IsNumeric;
        private bool _sortItem4IsNumeric;
        public bool SortItem1IsNumeric
        {
            get
            {
                return _sortItem1IsNumeric;
            }
            set
            {
                _sortItem1IsNumeric = value;
                RaisePropertyChanged();
            }
        }
        public bool SortItem2IsNumeric
        {
            get
            {
                return _sortItem2IsNumeric;
            }
            set
            {
                _sortItem2IsNumeric = value;
                RaisePropertyChanged();
            }
        }
        public bool SortItem3IsNumeric
        {
            get
            {
                return _sortItem3IsNumeric;
            }
            set
            {
                _sortItem3IsNumeric = value;
                RaisePropertyChanged();
            }
        }
        public bool SortItem4IsNumeric
        {
            get
            {
                return _sortItem4IsNumeric;
            }
            set
            {
                _sortItem4IsNumeric = value;
                RaisePropertyChanged();
            }
        }

        private string _splitItem1SelectedValue;
        private string _splitItem2SelectedValue;
        private string _splitItem3SelectedValue;
        private string _splitItem4SelectedValue;
        public string SplitItem1SelectedValue
        {
            get
            {
                return _splitItem1SelectedValue;
            }
            set
            {
                _splitItem1SelectedValue = value;
                RaisePropertyChanged();
            }
        }
        public string SplitItem2SelectedValue
        {
            get
            {
                return _splitItem2SelectedValue;
            }
            set
            {
                _splitItem2SelectedValue = value;
                RaisePropertyChanged();
            }
        }
        public string SplitItem3SelectedValue
        {
            get
            {
                return _splitItem3SelectedValue;
            }
            set
            {
                _splitItem3SelectedValue = value;
                RaisePropertyChanged();
            }
        }
        public string SplitItem4SelectedValue
        {
            get
            {
                return _splitItem4SelectedValue;
            }
            set
            {
                _splitItem4SelectedValue = value;
                RaisePropertyChanged();
            }
        }

        public bool IsProcessing
        {
            get
            {
                return _isProcessing;
            }
            private set
            {
                _isProcessing = value;
                RaisePropertyChanged();
                RaisePropertyChanged("IsAbleToInput");
                RaisePropertyChanged("IsAbleToInputIntegrateCheck");
            }
        }

        public string AnalysisReportOfInputFiles
        {
            get
            {
                return _analysisReportOfInputFiles;
            }
            set
            {
                _analysisReportOfInputFiles = value;
                RaisePropertyChanged();
            }
        }

        public bool IntegrationMode
        {
            get
            {
                return _integrationMode;
            }
            set
            {
                _integrationMode = value;
                RaisePropertyChanged();
            }
        }

        public string OutputFolder
        {
            get
            {
                return _outputFolder;
            }
            set
            {
                _outputFolder = value;
                RaisePropertyChanged();
            }
        }

        private long _progressValue;
        public long ProgressValue 
        {
            get
            {
                return _progressValue;
            }
            set
            {
                _progressValue = value;
                RaisePropertyChanged();
            }
        }
        
        private long _maxCsvRecords;
        public long MaxCsvRecords
        {
            get
            {
                return _maxCsvRecords;
            }
            set
            {
                _maxCsvRecords = value;
                RaisePropertyChanged();
            }
        }

        public string MaxCsvRecordsString
        {
            get
            {
                return MaxCsvRecords.ToString();
            }
            set
            {
                long tmp;
                if (long.TryParse(value, out tmp))
                {
                    if (tmp > 1)
                    {
                        MaxCsvRecords = tmp;
                    }
                }
                RaisePropertyChanged();
            }
        }

        public bool IsAbleToInput
        {
            get
            {
                return !_isProcessing;
            }
        }

        public bool IsAbleToInputIntegrateCheck
        {
            get
            {
                return IsAbleToInput && InputFiles.HasUniformHeaders && InputFiles.Count > 1;
            }
        }

        readonly DispatcherTimer timer;
        readonly Stopwatch stopwatch;
        private string _strTime;
        public string StrTime
        {
            get
            {
                return _strTime;
            }
            set
            {
                _strTime = value;
                RaisePropertyChanged();
            }
        }

        public MainWindowViewModel()
        {
            IsProcessing = false;
            InputFiles = new Models.InputFiles();
            SortItems = new Models.SortItems();
            SplitItems = new Models.SplitItems();
            TempFiles = new Models.TempFiles();
            SetInputFilesCommand = new SetInputFilesCommand(this);
            ResetCommand = new ResetCommand(this);
            AnalyzeInputFilesCommand = new AnalyzeInputFilesCommand(this);
            ConvertCommand = new ConvertCommand(this);
            OpenFolderCommand = new OpenFolderCommand(this);
            SelectFolderCommand = new SelectFolderCommand(this);
            SortItem1IsDescending = false;
            SortItem2IsDescending = false;
            SortItem3IsDescending = false;
            SortItem4IsDescending = false;
            SortItem1IsNumeric = false;
            SortItem2IsNumeric = false;
            SortItem3IsNumeric = false;
            SortItem4IsNumeric = false;
            SortItem1SelectedValue = "";
            SortItem2SelectedValue = "";
            SortItem3SelectedValue = "";
            SortItem4SelectedValue = "";
            IntegrationMode = false;
            OutputFolder = System.IO.Path.Combine(Utils.Utils.GetAssemblyPath(), "output");
            if (!System.IO.Directory.Exists(OutputFolder))
            {
                System.IO.Directory.CreateDirectory(OutputFolder);
            }
            ProgressValue = 0;
            MaxCsvRecords = Global.Const.DEFAULT_MAX_SPLITFILE_RECORDS;
            StrTime = "00:00:00.000";
            stopwatch = new Stopwatch();
            timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            timer.Tick += new EventHandler(TimerMethod);
            timer.Start();
        }

        public void AddInputFile(string filePath)
        {
            if(!IsAbleToInput)
            {
                return;
            }
            InputFiles.Add(filePath);
            //RaisePropertyChanged("InputFiles");
        }

        public void ClearInputFiles()
        {
            if (!IsAbleToInput)
            {
                return;
            }
            InputFiles.Clear();
            //RaisePropertyChanged("InputFiles");
        }
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if(propertyName == "StrTime") { return; }
            Utils.DebugTool.WriteLine(propertyName);
        }

        public void AnalyzeInputFiles()
        {
            InputFiles.Analyze();
            RaisePropertyChanged("InputFiles");
            
        }

        public void ChangeProcessingStatus(bool isProcessing)
        {
            IsProcessing = isProcessing;
        }

        public void Reset()
        {
            ClearInputFiles();
            AnalysisReportOfInputFiles = "";
            SortItems.Clear();
            SortItem1IsDescending = false;
            SortItem2IsDescending = false;
            SortItem3IsDescending = false;
            SortItem4IsDescending = false;
            SortItem1IsNumeric = false;
            SortItem2IsNumeric = false;
            SortItem3IsNumeric = false;
            SortItem4IsNumeric = false;
            SortItem1SelectedValue = "";
            SortItem2SelectedValue = "";
            SortItem3SelectedValue = "";
            SortItem4SelectedValue = "";
            SplitItems.Clear();
            IntegrationMode = false;
        }

        private void TimerMethod(object sender, EventArgs e)
        {
            var result = stopwatch.Elapsed;
            this.StrTime = 
                        result.Hours.ToString().PadLeft(2, '0')
                + ":" + result.Minutes.ToString().PadLeft(2, '0')
                + ":" + result.Seconds.ToString().PadLeft(2, '0')
                + "." + result.Milliseconds.ToString().PadLeft(3, '0');
        }

        public void StartStopwatch()
        {
            stopwatch.Start();
        }

        public void StopStopwatch()
        {
            stopwatch.Stop();
        }

        public void ResetStopwatch()
        {
            stopwatch.Reset();
            StrTime = "00:00:00.000";
        }

    }
}
