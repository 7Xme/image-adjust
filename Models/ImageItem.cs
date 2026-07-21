using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;

namespace ImageAdjust.Models
{
    public partial class ImageItem : ObservableObject
    {
        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private BitmapSource? _thumbnail;

        [ObservableProperty]
        private bool _isSelected;
    }
}
