namespace ImageAdjust.Models
{
    public class CardTemplateProfile
    {
        public double MinRedness { get; set; } = 28;
        public double MinRedIntensity { get; set; } = 55;
        public double CardAspectRatio { get; set; } = 856.0 / 540.0;
        public int TemplateCount { get; set; }
        public bool HasTemplates => TemplateCount > 0;
    }
}
