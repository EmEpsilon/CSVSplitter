using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSVSplitter.Commands
{
    public class OpenFolderCommand : ICommand
    {
        private ViewModels.MainWindowViewModel _viewModel;

        public OpenFolderCommand(ViewModels.MainWindowViewModel viewModel)
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

        public void Execute(object parameter)
        {
            try
            {
                if (System.IO.Directory.Exists(this._viewModel.OutputFolder))
                {
                    System.Diagnostics.Process.Start(this._viewModel.OutputFolder);
                }
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show("フォルダを開く際にエラーが発生しました: " + e.Message);
                Utils.DebugTool.WriteLine("Error opening folder: " + e.ToString(), true);
                throw e;
            }
        }
    }
}
