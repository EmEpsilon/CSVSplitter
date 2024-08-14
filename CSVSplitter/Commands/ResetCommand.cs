using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSVSplitter.Commands
{
    public class ResetCommand : ICommand
    {
        private ViewModels.MainWindowViewModel _viewModel;

        public ResetCommand(ViewModels.MainWindowViewModel viewModel)
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
            Utils.DebugTool.WriteLine("Execute Reset");
            this._viewModel.Reset();
        }
    }
}
