using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSVSplitter.Commands
{
    public class SetInputFilesCommand : ICommand
    {
        private ViewModels.MainWindowViewModel _viewModel;

        public SetInputFilesCommand(ViewModels.MainWindowViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return 
                !this._viewModel.IsProcessing;
        }

        public void Execute(object parameter)
        {
            Utils.DebugTool.WriteLine("Execute SetInputFiles");

            if(parameter is string[])
            {
                var files = parameter as string[];
                foreach (var file in files)
                {
                    if (!_viewModel.InputFiles.Contains(file))
                    {
                        _viewModel.AddInputFile(file);
                    }
                }
            }
        }
    }
}
