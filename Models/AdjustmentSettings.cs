using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageAdjust.Models
{
    public partial class AdjustmentSettings : ObservableObject
    {
        [ObservableProperty]
        private int _shadows;

        [ObservableProperty]
        private int _highlights;

        [ObservableProperty]
        private int _saturation;

        [ObservableProperty]
        private int _contrast;

        public AdjustmentSettings Clone()
        {
            return new AdjustmentSettings
            {
                Shadows = Shadows,
                Highlights = Highlights,
                Saturation = Saturation,
                Contrast = Contrast
            };
        }

        public bool IsDefault => Shadows == 0 && Highlights == 0 && Saturation == 0 && Contrast == 0;
    }
}
