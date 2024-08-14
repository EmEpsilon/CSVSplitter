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
            if (System.IO.Directory.Exists(this._viewModel.OutputFolder))
            {
                System.Diagnostics.Process.Start(this._viewModel.OutputFolder);
            }
        }
    }
}
