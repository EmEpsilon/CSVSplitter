using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSVSplitter.Commands
{
    public class AnalyzeInputFilesCommand:ICommand
    {
        private ViewModels.MainWindowViewModel _viewModel;

        public AnalyzeInputFilesCommand(ViewModels.MainWindowViewModel viewModel)
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
                && !this._viewModel.IsProcessing;
        }

        public void Execute(object parameter)
        {
            Utils.DebugTool.WriteLine("Execute AnalyzeInputFiles");
            this._viewModel.AnalyzeInputFiles();
            this._viewModel.AnalysisReportOfInputFiles = this._viewModel.InputFiles.GetAnalysisReport();
            var commonHeaders = this._viewModel.InputFiles.GetCommonHeaders();
            this._viewModel.SortItems.Clear();
            this._viewModel.SortItems.Add("");
            foreach (var header in commonHeaders)
            {
                this._viewModel.SortItems.Add(header);
            }
            this._viewModel.SplitItems.Clear();
            this._viewModel.SplitItems.Add("");
            foreach (var header in commonHeaders)
            {
                this._viewModel.SplitItems.Add(header);
            }
            this._viewModel.IntegrationMode = false;
        }
    }
}
