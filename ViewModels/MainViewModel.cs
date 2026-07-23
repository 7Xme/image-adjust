using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageAdjust.Models;
using ImageAdjust.Services;
using ImageAdjust.Views;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ImageAdjust.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ImageProcessingService _imageService = new();

        [ObservableProperty]
        private ObservableCollection<ImageItem> _images = new();

        [ObservableProperty]
        private int _selectedCount;

        [ObservableProperty]
        private bool _hasSingleSelection;

        [ObservableProperty]
        private bool _hasDoubleSelection;

        [ObservableProperty]
        private string _folderPath = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" };

        partial void OnImagesChanged(ObservableCollection<ImageItem> value)
        {
            UpdateSelectionState();
        }

        private void UpdateSelectionState()
        {
            SelectedCount = Images.Count(i => i.IsSelected);
            HasSingleSelection = SelectedCount == 1;
            HasDoubleSelection = SelectedCount == 2;
        }

        public void OnSelectionChanged()
        {
            UpdateSelectionState();
        }

        [RelayCommand]
        private async Task OpenFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            dialog.Title = "اختر مجلد الصور / Sélectionnez le dossier d'images";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
            if (dialog.ShowDialog() == true)
            {
                await LoadFolder(dialog.FolderName);
            }
        }

        public async Task LoadFolder(string path)
        {
            FolderPath = path;
            IsLoading = true;
            Images.Clear();

            var files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

            var items = new List<ImageItem>();

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    try
                    {
                        using var bitmap = SKBitmap.Decode(file);
                        if (bitmap == null) continue;

                        var thumbnail = _imageService.CreateThumbnail(bitmap);
                        var item = new ImageItem
                        {
                            FilePath = file,
                            FileName = Path.GetFileName(file),
                            Thumbnail = thumbnail
                        };
                        items.Add(item);
                    }
                    catch { }
                }
            });

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var item in items)
                {
                    Images.Add(item);
                }
                IsLoading = false;
            });
        }

        [RelayCommand]
        private void OpenAdjust()
        {
            var selected = Images.FirstOrDefault(i => i.IsSelected);
            if (selected == null) return;

            var editWindow = new EditWindow(selected.FilePath);
            editWindow.Owner = Application.Current.MainWindow;
            editWindow.ShowDialog();
        }

        [RelayCommand]
        private void PrintSingle()
        {
            var selected = Images.FirstOrDefault(i => i.IsSelected);
            if (selected == null) return;

            try
            {
                using var bitmap = SKBitmap.Decode(selected.FilePath);
                using var adjusted = _imageService.ApplyAdjustments(bitmap, new AdjustmentSettings());
                var bmp = _imageService.ToBitmapSource(adjusted);

                var printService = new PrintService();
                printService.PrintImageDirect(bmp, $"طباعة - {selected.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الطباعة / Erreur d'impression: {ex.Message}",
                    "خطأ / Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenIdCard()
        {
            var selected = Images.Where(i => i.IsSelected).ToList();
            if (selected.Count != 2) return;

            var idCardWindow = new IdCardWindow(selected[0].FilePath, selected[1].FilePath);
            idCardWindow.Owner = Application.Current.MainWindow;
            idCardWindow.ShowDialog();
        }

        [RelayCommand]
        private void ToggleSelection(ImageItem? item)
        {
            if (item == null) return;
            item.IsSelected = !item.IsSelected;
            UpdateSelectionState();
        }
    }
}
