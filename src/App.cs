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
        bool view3DTest = args.Length > 0 && args[0] == "--test-3d-view";
        bool editorSoundsTest = args.Length > 0 && args[0] == "--test-editor-sounds";
        bool geometryToolsTest = args.Length > 0 && args[0] == "--test-geometry-tools";
        bool selectionFocusTest = args.Length > 0 && args[0] == "--test-selection-focus";
        bool editorLayoutTest = args.Length > 0 && args[0] == "--test-editor-layout";
        bool contextOptionsTest = args.Length > 0 && args[0] == "--test-context-options";
        bool feedbackTest = args.Length > 0 && args[0] == "--test-interaction-feedback";
        bool rtsTest = args.Length > 0 && args[0] == "--test-rts";
        bool presentationTest = args.Length > 0 && args[0] is "--test-presented-fps" or "--test-presented-fps-unavailable";
        if (view3DTest || editorSoundsTest || geometryToolsTest || selectionFocusTest || editorLayoutTest || contextOptionsTest || feedbackTest || rtsTest || presentationTest || args.Contains("--self-test"))
        {
            try
            {
                string output = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "test-output");
                if (presentationTest) PresentedFpsTests.Run(output, args[0] == "--test-presented-fps-unavailable");
                else if (view3DTest) View3DTests.Run(output);
                else if (editorSoundsTest) EditorSoundTests.Run(output);
                else if (geometryToolsTest) GeometryToolTests.Run(output);
                else if (selectionFocusTest) SelectionFocusTests.Run(output);
                else if (editorLayoutTest) EditorLayoutTests.Run(output);
                else if (contextOptionsTest) ContextOptionsTests.Run(output);
                else if (feedbackTest) InteractionFeedbackTests.Run(output);
                else if (rtsTest) RtsTests.Run(output);
                else SelfTests.Run(output);
                return 0;
            }
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
