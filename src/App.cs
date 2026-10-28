// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Coat;

public static class App
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            try { SelfTests.Run(args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "test-output")); return 0; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "test-error.txt"), ex.ToString()); return 1; }
        }
        var app = new Application();
        app.DispatcherUnhandledException += (_, e) =>
        {
            AppDialog.Show(L.T("Coat a rencontré un problème :\n\n") + e.Exception.Message + L.T("\n\nEnregistrez votre plan dans un nouveau fichier si possible."), "Coat", MessageBoxButton.OK, MessageBoxImage.Error); e.Handled = true;
        };
        var window = new MainWindow(); window.LoadPreferences(); window.LoadPalette();
        if (args.Length > 0 && File.Exists(args[0])) window.Loaded += (_, _) => window.OpenPath(Path.GetFullPath(args[0]));
        return app.Run(window);
    }
}
