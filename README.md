# Image Adjust

تطبيق سطح مكتب احترافي لتحرير وطباعة صور البطاقة الوطنية (CIN)
Application de bureau professionnelle pour le traitement et l'impression des photos de la Carte d'Identité Nationale (CIN)
Professional Windows desktop application for editing and printing Moroccan National Identity Card (CIN) images.

## Requirements / المتطلبات / Prérequis

- Windows 10 or later (64-bit)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Quick Start / بدء سريع / Démarrage rapide

```bash
# Clone the repository
git clone <repo-url>
cd ImageAdjust

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run --project ImageAdjust.csproj
```

Or open `ImageAdjust.slnx` in Visual Studio 2022 and press F5.

## Usage / كيفية الاستخدام / Utilisation

### 1. Open a Folder / فتح مجلد / Ouvrir un dossier
- Click **"📂 فتح مجلد / Ouvrir un dossier"** to browse for a folder containing images
- Or drag-and-drop a folder onto the window
- Supported formats: JPG, PNG, BMP

### 2. Select Images / اختيار الصور / Sélectionner des images
- Click one image to select it for **editing or printing**
- Click two images to select them for **ID card printing** (front + back)

### 3. Single Image Editing / تحرير صورة واحدة / Modifier une seule image
- Click **"🎨 تعديل / Ajuster"** to open the editor
- Use sliders to adjust: **Shadows**, **Highlights**, **Saturation**, **Contrast**
- Zoom in/out for detailed inspection
- **💾 حفظ / Enregistrer** — save adjusted image to a new file
- **🖨️ طباعة / Imprimer** — print directly

### 4. ID Card Printing / طباعة البطاقة / Impression de la carte
- Select two images and click **"🪪 طباعة كبطاقة تعريف / Imprimer comme CIN"**
- View front (recto) and back (verso) side-by-side
- Adjust both images simultaneously with the same controls
- Toggle **crop mode** to fine-tune card boundaries
- Click **"🖨️ طباعة البطاقة / Imprimer la carte"** to generate and print a 2-page PDF
  - Page 1 = Front of card (85.6mm × 54mm — CR-80 / ISO/IEC 7810 ID-1)
  - Page 2 = Back of card

### 5. Print Output / مخرجات الطباعة / Sortie d'impression
- The app opens the system print dialog
- Alternatively, the PDF is saved temporarily and opened with the default PDF viewer

## Project Structure / هيكل المشروع / Structure du projet

```
ImageAdjust/
├── Models/              # Data models (ImageItem, AdjustmentSettings, CropRegion)
├── ViewModels/          # MVVM ViewModels (Main, Edit, IdCard)
├── Views/               # XAML windows (MainWindow, EditWindow, IdCardWindow)
├── Services/            # Business logic (ImageProcessing, Pdf, Print)
├── Converters/          # XAML value converters
├── Styles/              # XAML resource dictionaries
└── App.xaml             # Application entry point
```

## Contributing / المساهمة / Contribution

Contributions are welcome! Please follow these steps:

1. **Fork** the repository
2. **Create a feature branch**: `git checkout -b feature/my-feature`
3. **Commit your changes**: `git commit -am 'Add my feature'`
4. **Push the branch**: `git push origin feature/my-feature`
5. **Open a Pull Request**

### Guidelines

- Follow MVVM patterns — keep logic in ViewModels/Services, not code-behind
- Use Arabic (RTL) and French labels for UI text
- Use `dotnet build` to verify your code compiles before submitting
- Test with various image formats and resolutions
- Keep the UI clean and consistent with existing styles

### Code Style
- C# with nullable reference types enabled
- XAML with explicit resource keys
- `.editorconfig` included in the solution

## Tech Stack / التقنيات المستخدمة / Technologies utilisées

| Technology | Purpose |
|---|---|
| .NET 8.0 (WPF) | Application framework |
| CommunityToolkit.Mvvm | MVVM pattern (ObservableObject, RelayCommand) |
| SkiaSharp | Image processing (adjustments, resizing, cropping) |
| PdfSharp 6.x | PDF generation for 2-page card output |
| System.Printing | Print dialog and document handling |

## License

MIT
