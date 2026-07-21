using ImageAdjust.ViewModels;
using System.Windows;

namespace ImageAdjust.Views
{
    public partial class EditWindow : Window
    {
        public EditViewModel ViewModel { get; }

        public EditWindow(string filePath)
        {
            InitializeComponent();
            ViewModel = new EditViewModel(filePath);
            DataContext = ViewModel;
        }
    }
}
