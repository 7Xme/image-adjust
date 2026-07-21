using ImageAdjust.ViewModels;
using System.Windows;

namespace ImageAdjust.Views
{
    public partial class IdCardWindow : Window
    {
        public IdCardViewModel ViewModel { get; }

        public IdCardWindow(string frontPath, string backPath)
        {
            InitializeComponent();
            ViewModel = new IdCardViewModel(frontPath, backPath);
            DataContext = ViewModel;
        }
    }
}
