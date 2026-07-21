using ImageAdjust.Models;
using ImageAdjust.ViewModels;
using Wpf = System.Windows;

namespace ImageAdjust.Views
{
    public partial class MainWindow : Wpf.Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;
        }

        private void Thumbnail_MouseDown(object sender, Wpf.Input.MouseButtonEventArgs e)
        {
            if (sender is Wpf.FrameworkElement element && element.DataContext is ImageItem item)
            {
                ViewModel.ToggleSelectionCommand.Execute(item);
            }
        }

        protected override void OnPreviewDrop(Wpf.DragEventArgs e)
        {
            base.OnPreviewDrop(e);
            if (e.Data.GetDataPresent(Wpf.DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(Wpf.DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    var dir = System.IO.Path.GetDirectoryName(files[0]);
                    if (dir != null)
                    {
                        _ = ViewModel.LoadFolder(dir);
                    }
                }
            }
        }

        protected override void OnPreviewDragOver(Wpf.DragEventArgs e)
        {
            base.OnPreviewDragOver(e);
            if (e.Data.GetDataPresent(Wpf.DataFormats.FileDrop))
            {
                e.Effects = Wpf.DragDropEffects.Copy;
                e.Handled = true;
            }
        }
    }
}
