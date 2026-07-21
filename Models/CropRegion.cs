using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageAdjust.Models
{
    public partial class CropRegion : ObservableObject
    {
        [ObservableProperty]
        private double _x;

        [ObservableProperty]
        private double _y;

        [ObservableProperty]
        private double _width;

        [ObservableProperty]
        private double _height;

        public CropRegion Clone()
        {
            return new CropRegion { X = X, Y = Y, Width = Width, Height = Height };
        }

        public void Set(double x, double y, double w, double h)
        {
            X = x; Y = y; Width = w; Height = h;
        }
    }
}
