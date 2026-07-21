using ImageAdjust.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ImageAdjust.Views
{
    public partial class EditWindow : Window
    {
        public EditViewModel ViewModel { get; }

        public EditWindow(string filePath)
        {
            ViewModel = new EditViewModel(filePath);
            DataContext = ViewModel;
            BuildUI();
        }

        private void BuildUI()
        {
            Title = "Image Adjust - Edit";
            Height = 700;
            Width = 1100;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = System.Windows.ResizeMode.CanResize;

            var rootGrid = new Grid();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            rootGrid.Children.Add(BuildPreviewPanel());
            rootGrid.Children.Add(BuildAdjustmentPanel());

            Grid.SetColumn(rootGrid.Children[1], 1);

            Content = rootGrid;
        }

        private FrameworkElement BuildPreviewPanel()
        {
            var grid = new Grid();
            grid.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var zoomBar = new Border { Background = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x3D)), Padding = new Thickness(8) };
            var zoomStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            var zoomOutBtn = new Button { Content = "\U0001f50d-", Style = FindResource("SmallButton") as Style, Margin = new Thickness(4, 0, 4, 0) };
            zoomOutBtn.SetBinding(Button.CommandProperty, "ZoomOutCommand");
            zoomStack.Children.Add(zoomOutBtn);

            var zoomText = new TextBlock { Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0), FontSize = 13, FontWeight = FontWeights.SemiBold };
            zoomText.SetBinding(TextBlock.TextProperty, new Binding("ZoomLevel") { StringFormat = "{0:P0}" });
            zoomStack.Children.Add(zoomText);

            var zoomInBtn = new Button { Content = "\U0001f50d+", Style = FindResource("SmallButton") as Style, Margin = new Thickness(4, 0, 4, 0) };
            zoomInBtn.SetBinding(Button.CommandProperty, "ZoomInCommand");
            zoomStack.Children.Add(zoomInBtn);

            var resetZoomBtn = new Button { Content = "\u0625\u0639\u0627\u062f\u0629 / R\u00e9initialiser", Style = FindResource("SmallButton") as Style, Margin = new Thickness(16, 0, 4, 0) };
            resetZoomBtn.SetBinding(Button.CommandProperty, "ResetZoomCommand");
            zoomStack.Children.Add(resetZoomBtn);

            zoomBar.Child = zoomStack;
            Grid.SetRow(zoomBar, 0);
            grid.Children.Add(zoomBar);

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
            var image = new System.Windows.Controls.Image { Stretch = System.Windows.Media.Stretch.Uniform, RenderTransformOrigin = new Point(0.5, 0.5), Margin = new Thickness(20) };
            image.SetBinding(System.Windows.Controls.Image.SourceProperty, "PreviewImage");
            var scaleTransform = new ScaleTransform();
            BindingOperations.SetBinding(scaleTransform, ScaleTransform.ScaleXProperty, new Binding("ZoomLevel"));
            BindingOperations.SetBinding(scaleTransform, ScaleTransform.ScaleYProperty, new Binding("ZoomLevel"));
            image.RenderTransform = scaleTransform;
            scrollViewer.Content = image;
            Grid.SetRow(scrollViewer, 1);
            grid.Children.Add(scrollViewer);

            return grid;
        }

        private FrameworkElement BuildAdjustmentPanel()
        {
            var border = new Border { Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)), Width = 320, Padding = new Thickness(16) };
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            border.BorderThickness = new Thickness(1, 0, 0, 0);

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel();

            var header = new TextBlock
            {
                Text = "\U0001f3a8 \u0625\u0639\u062f\u0627\u062f\u0627\u062a \u0627\u0644\u062a\u0639\u062f\u064a\u0644 / Param\u00e8tres d'ajustement",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                Margin = new Thickness(0, 0, 0, 20)
            };
            stack.Children.Add(header);

            AddSlider(stack, "\u0627\u0644\u0638\u0644\u0627\u0644 / Ombres (Shadows)", "Settings.Shadows");
            AddSlider(stack, "\u0627\u0644\u0625\u0636\u0627\u0621\u0629 / Hautes lumi\u00e8res (Highlights)", "Settings.Highlights");
            AddSlider(stack, "\u0627\u0644\u062a\u0634\u0628\u0639 / Saturation", "Settings.Saturation");
            AddSlider(stack, "\u0627\u0644\u062a\u0628\u0627\u064a\u0646 / Contraste", "Settings.Contrast");

            var sep = new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), Margin = new Thickness(0, 0, 0, 16) };
            stack.Children.Add(sep);

            AddButton(stack, "\U0001f4be \u062d\u0641\u0638 \u0627\u0644\u0635\u0648\u0631\u0629 / Enregistrer", "SaveImageCommand", "PrimaryButton", 44);
            AddButton(stack, "\U0001f5a8\ufe0f \u0637\u0628\u0627\u0639\u0629 / Imprimer", "PrintImageCommand", "ActionButton", 44);
            AddButton(stack, "\U0001f504 \u0625\u0639\u0627\u062f\u0629 \u0627\u0644\u0636\u0628\u0637 / R\u00e9initialiser", "ResetAdjustmentsCommand", "SecondaryButton", 36);

            var cancelBtn = new Button
            {
                Content = "\u2716 \u0625\u0644\u063a\u0627\u0621 / Annuler",
                Style = FindResource("SecondaryButton") as Style,
                Height = 36,
                IsCancel = true
            };
            stack.Children.Add(cancelBtn);

            scrollViewer.Content = stack;
            border.Child = scrollViewer;
            return border;
        }

        private void AddSlider(StackPanel parent, string label, string bindingPath)
        {
            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55))
            };
            parent.Children.Add(labelText);

            var slider = new Slider
            {
                Minimum = -100,
                Maximum = 100,
                TickFrequency = 5,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 4, 0, 4)
            };
            slider.SetBinding(Slider.ValueProperty, new Binding(bindingPath) { Mode = BindingMode.TwoWay });
            parent.Children.Add(slider);

            var valueText = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            valueText.SetBinding(TextBlock.TextProperty, new Binding(bindingPath) { StringFormat = "{}{0}" });
            parent.Children.Add(valueText);
        }

        private void AddButton(StackPanel parent, string content, string commandPath, string styleKey, double height)
        {
            var btn = new Button
            {
                Content = content,
                Style = FindResource(styleKey) as Style,
                Height = height,
                Margin = new Thickness(0, 0, 0, 8)
            };
            btn.SetBinding(Button.CommandProperty, commandPath);
            parent.Children.Add(btn);
        }
    }
}
