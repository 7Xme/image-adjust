using ImageAdjust.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ImageAdjust.Views
{
    public partial class IdCardWindow : Window
    {
        public IdCardViewModel ViewModel { get; }

        public IdCardWindow(string frontPath, string backPath)
        {
            ViewModel = new IdCardViewModel(frontPath, backPath);
            DataContext = ViewModel;
            BuildUI();
        }

        private void BuildUI()
        {
            Title = "Image Adjust - ID Card";
            Height = 800;
            Width = 1200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            rootGrid.Children.Add(BuildPreviewsPanel());
            rootGrid.Children.Add(BuildAdjustmentsPanel());
            rootGrid.Children.Add(BuildBottomBar());

            Grid.SetRow(rootGrid.Children[1], 1);
            Grid.SetRow(rootGrid.Children[2], 2);

            var overlay = BuildProcessingOverlay();
            Grid.SetRowSpan(overlay, 3);
            rootGrid.Children.Add(overlay);

            Content = rootGrid;
        }

        private FrameworkElement BuildPreviewsPanel()
        {
            var grid = new Grid { Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            grid.Children.Add(BuildCardPreview("FrontPreview", "IsCroppingFront", "#00FF88",
                "\u0627\u0644\u0648\u062c\u0647 \u0627\u0644\u0623\u0645\u0627\u0645\u064a / Face avant (Recto)",
                "ToggleCropFrontCommand", 0));
            grid.Children.Add(BuildSeparator(1));
            grid.Children.Add(BuildCardPreview("BackPreview", "IsCroppingBack", "#FF8800",
                "\u0627\u0644\u0648\u062c\u0647 \u0627\u0644\u062e\u0644\u0641\u064a / Face arri\u00e8re (Verso)",
                "ToggleCropBackCommand", 2));

            return grid;
        }

        private Border BuildCardPreview(string imageBinding, string cropBinding, string cropColor, string title, string toggleCommand, int column)
        {
            var border = new Border { Margin = new Thickness(12) };
            Grid.SetColumn(border, column);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x3D)),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(6, 6, 0, 0)
            };
            var headerText = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 14
            };
            headerBorder.Child = headerText;
            Grid.SetRow(headerBorder, 0);
            grid.Children.Add(headerBorder);

            var contentBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                CornerRadius = new CornerRadius(0, 0, 6, 6)
            };
            var contentGrid = new Grid();
            var image = new System.Windows.Controls.Image { Stretch = Stretch.Uniform, Margin = new Thickness(16) };
            image.SetBinding(System.Windows.Controls.Image.SourceProperty, imageBinding);
            contentGrid.Children.Add(image);

            var cropBorder = new Border
            {
                BorderBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(cropColor)!,
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(16)
            };
            cropBorder.SetBinding(Border.VisibilityProperty, new Binding(cropBinding) { Converter = Application.Current.FindResource("BoolToVis") as IValueConverter });
            contentGrid.Children.Add(cropBorder);

            contentBorder.Child = contentGrid;
            Grid.SetRow(contentBorder, 1);
            grid.Children.Add(contentBorder);

            var cropBtn = new Button
            {
                Content = "\u2702\ufe0f \u0642\u0635 / Rogner",
                Style = FindResource("SmallButton") as Style,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 8, 0),
                Opacity = 0.9
            };
            cropBtn.SetBinding(Button.CommandProperty, toggleCommand);
            grid.Children.Add(cropBtn);

            border.Child = grid;
            return border;
        }

        private Border BuildSeparator(int column)
        {
            var sep = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                Width = 4
            };
            Grid.SetColumn(sep, column);
            return sep;
        }

        private Border BuildAdjustmentsPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)),
                Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                BorderThickness = new Thickness(0, 1, 0, 1)
            };

            var grid = new Grid();
            var starWidth = new GridLength(1, GridUnitType.Star);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = starWidth });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = starWidth });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = starWidth });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = starWidth });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            AddSmallSlider(grid, 0, "\u0627\u0644\u0638\u0644\u0627\u0644 / Ombres", "Settings.Shadows");
            AddSmallSlider(grid, 1, "\u0627\u0644\u0625\u0636\u0627\u0621\u0629 / Hautes lumi\u00e8res", "Settings.Highlights");
            AddSmallSlider(grid, 2, "\u0627\u0644\u062a\u0634\u0628\u0639 / Saturation", "Settings.Saturation");
            AddSmallSlider(grid, 3, "\u0627\u0644\u062a\u0628\u0627\u064a\u0646 / Contraste", "Settings.Contrast");

            var btnStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(btnStack, 4);

            var resetBtn = new Button
            {
                Content = "\U0001f504 \u0625\u0639\u0627\u062f\u0629 / Reset",
                Style = FindResource("SecondaryButton") as Style,
                Height = 30,
                Margin = new Thickness(0, 0, 0, 4)
            };
            resetBtn.SetBinding(Button.CommandProperty, "ResetAdjustmentsCommand");
            btnStack.Children.Add(resetBtn);

            var resetCropBtn = new Button
            {
                Content = "\u2702\ufe0f \u0625\u0639\u0627\u062f\u0629 \u0627\u0644\u0642\u0635 / Reset Crop",
                Style = FindResource("SecondaryButton") as Style,
                Height = 30
            };
            resetCropBtn.SetBinding(Button.CommandProperty, "ResetCropCommand");
            btnStack.Children.Add(resetCropBtn);

            grid.Children.Add(btnStack);
            border.Child = grid;
            return border;
        }

        private void AddSmallSlider(Grid parent, int column, string label, string bindingPath)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            Grid.SetColumn(stack, column);

            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55))
            };
            stack.Children.Add(labelText);

            var slider = new Slider
            {
                Minimum = -100,
                Maximum = 100,
                TickFrequency = 5,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 4, 0, 4)
            };
            slider.SetBinding(Slider.ValueProperty, new Binding(bindingPath) { Mode = BindingMode.TwoWay });
            stack.Children.Add(slider);

            var valueText = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            valueText.SetBinding(TextBlock.TextProperty, new Binding(bindingPath) { StringFormat = "{}{0}" });
            stack.Children.Add(valueText);

            parent.Children.Add(stack);
        }

        private Border BuildBottomBar()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)),
                Padding = new Thickness(16, 12, 16, 12)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoText = new TextBlock
            {
                Text = "\U0001f4a1 \u0633\u064a\u062a\u0645 \u0637\u0628\u0627\u0639\u0629 \u0648\u062c\u0647\u064a\u0646: \u0627\u0644\u0635\u0641\u062d\u0629 \u0627\u0644\u0623\u0648\u0644\u0649 = \u0627\u0644\u0648\u062c\u0647 \u0627\u0644\u0623\u0645\u0627\u0645\u064a\u060c \u0627\u0644\u0635\u0641\u062d\u0629 \u0627\u0644\u062b\u0627\u0646\u064a\u0629 = \u0627\u0644\u0648\u062c\u0647 \u0627\u0644\u062e\u0644\u0641\u064a / Impression recto-verso : Page 1 = Recto, Page 2 = Verso",
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(infoText);

            var printBtn = new Button
            {
                Content = "\U0001f5a8\ufe0f \u0637\u0628\u0627\u0639\u0629 \u0627\u0644\u0628\u0637\u0627\u0642\u0629 / Imprimer la carte",
                Style = FindResource("PrimaryButton") as Style,
                Height = 40,
                Padding = new Thickness(24, 8, 24, 8),
                Margin = new Thickness(0, 0, 12, 0)
            };
            printBtn.SetBinding(Button.CommandProperty, "PrintIdCardCommand");
            Grid.SetColumn(printBtn, 1);
            grid.Children.Add(printBtn);

            var savePdfBtn = new Button
            {
                Content = "\U0001f4be \u062d\u0641\u0638 PDF / Enregistrer PDF",
                Style = FindResource("ActionButton") as Style,
                Height = 40,
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 12, 0)
            };
            savePdfBtn.SetBinding(Button.CommandProperty, "SavePdfCommand");
            Grid.SetColumn(savePdfBtn, 2);
            grid.Children.Add(savePdfBtn);

            var cancelBtn = new Button
            {
                Content = "\u2716 \u0625\u0644\u063a\u0627\u0621 / Annuler",
                Style = FindResource("SecondaryButton") as Style,
                Height = 40,
                Padding = new Thickness(16, 8, 16, 8),
                IsCancel = true
            };
            Grid.SetColumn(cancelBtn, 3);
            grid.Children.Add(cancelBtn);

            border.Child = grid;
            return border;
        }

        private Border BuildProcessingOverlay()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF))
            };
            border.SetBinding(Border.VisibilityProperty, new Binding("IsProcessing")
            {
                Converter = Application.Current.FindResource("BoolToVis") as IValueConverter
            });

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var text = new TextBlock
            {
                Text = "\u062c\u0627\u0631\u064a \u0627\u0644\u0645\u0639\u0627\u0644\u062c\u0629... / Traitement en cours...",
                FontSize = 20,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
                FontWeight = FontWeights.Bold
            };
            stack.Children.Add(text);
            border.Child = stack;
            return border;
        }
    }
}
