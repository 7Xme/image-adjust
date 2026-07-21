using System.Windows;

namespace ImageAdjust
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += (_, args) =>
            {
                MessageBox.Show($"Error: {args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                MessageBox.Show($"Fatal: {args.ExceptionObject}",
                    "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            base.OnStartup(e);
        }
    }
}
