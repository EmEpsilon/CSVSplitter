using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CSVSplitter.ViewModels;
using CSVSplitter.Models;

namespace CSVSplitter
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void GboxInputFile_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var vm = MainWindow1.DataContext;
                if (vm is MainWindowViewModel)
                {
                    if (((MainWindowViewModel)vm).SetInputFilesCommand.CanExecute((string[])e.Data.GetData(DataFormats.FileDrop)))
                    {
                        ((MainWindowViewModel)vm).SetInputFilesCommand.Execute((string[])e.Data.GetData(DataFormats.FileDrop));
                    }
                }
            }
        }

        private void GboxInputFile_DragOver(object sender, DragEventArgs e)
        {
            if(e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.All;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }
    }
}
