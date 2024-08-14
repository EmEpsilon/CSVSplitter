using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSVSplitter.Commands
{
    public class SelectFolderCommand : ICommand
    {
        private ViewModels.MainWindowViewModel _viewModel;

        public SelectFolderCommand(ViewModels.MainWindowViewModel viewModel)
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
            var dlg = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
            dlg.IsFolderPicker = true;
            dlg.Title = "出力先フォルダを選択してください。";
            dlg.InitialDirectory = this._viewModel.OutputFolder;
            if (dlg.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
            {
                this._viewModel.OutputFolder = dlg.FileName;
            }
        }
    }
}
