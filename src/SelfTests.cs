// Coat — Epicteta Lupercal
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Coat;

public static class SelfTests
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output); var results = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); results.Add("PASS: " + name); }
        Check(L.Language == "en", "English is the application default language"); L.SetLanguage("fr");
        Plan Square()
        {
            var p = new Plan(); p.Segment(new(0, 0), new(128, 0)); p.Segment(new(128, 0), new(128, 128)); p.Segment(new(128, 128), new(0, 128)); p.Segment(new(0, 128), new(0, 0)); return p;
        }
        Check(new V2(-17, 49).Snap(32) == new V2(-32, 64), "Grid snap including negative coordinates");
        var p = Square(); Check(p.Vertices.Count == 4 && p.Edges.Count == 4 && p.Faces().Count == 1 && p.Faces()[0].Area == 16384, "Closed contour becomes one face with correct area");
        Check(p.HitFace(new(64, 64)) != null && p.HitFace(new(200, 200)) == null, "Face hit testing");
        p.Fills[p.Faces()[0].Key] = "#66C5AD"; p.Segment(new(64, 0), new(64, 128));
        Check(p.Faces().Count == 2 && p.Edges.Count == 7 && p.Fills.Count == 2, "Partition a painted face, split T junctions, retain both fills");
        p = new Plan(); p.Segment(new(0, 0), new(128, 128)); p.Segment(new(0, 128), new(128, 0));
        Check(p.Vertices.Count == 5 && p.Edges.Count == 4 && p.Faces().Count == 0, "Crossing segments split into four edges");
        p.Segment(new(0, 0), new(128, 128)); Check(p.Edges.Count == 4, "Duplicate segments are merged");
        p = new Plan(); p.Segment(new(0, 0), new(128, 0)); p.Segment(new(32, 0), new(96, 0)); Check(p.Edges.Count == 3 && p.Vertices.Count == 4, "Collinear overlaps are split without duplicate edges");
        p = new Plan(); p.Segment(new(0, 0), new(128, 0)); int edgeId = p.Edges[0].Id; var extruded = p.Extrude(new[] { edgeId }, new V2(0, 32));
        Check(p.Edges.Count == 4 && p.Faces().Count == 1 && p.Faces()[0].Area == 4096 && extruded.Count == 1, "Edge extrusion creates a closed wall with requested thickness");
        var history = new History(); var before = p.Serialize(); history.Push(before); p.Get(p.Edges[0].A).X -= 32; var changed = p.Serialize();
        p = history.Undo(p)!; Check(p.Serialize() == before, "Undo restores geometry exactly"); p = history.Redo(p)!; Check(p.Serialize() == changed, "Redo restores geometry exactly");
        p = Square(); var originalKey = p.Faces()[0].Key; p.Fills[originalKey] = "#123456"; foreach (var v in p.Vertices) v.P += new V2(64, -32); p.Normalize(); Check(p.Fills[originalKey] == "#123456", "Moving a face preserves its fill");
        p.Edges.RemoveAt(0); p.Normalize(); Check(p.Faces().Count == 0 && p.Fills.Count == 0, "Deleting a contour edge opens the face and clears stale fills");
        p = Square(); p.Segment(new(0, 64), new(64, 64)); Check(p.HitFace(new(96, 96)) != null, "Interior dangling wall does not prevent face selection");
        var parsed = Plan.Parse(p.Serialize()); Check(parsed.Serialize() == p.Serialize(), "JSON round trip preserves the plan");
        bool rejected = false; try { Plan.Parse("{\"Version\":7}"); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Reject unsupported file versions");
        rejected = false; try { Plan.Parse("{\"Edges\":[{\"Id\":3,\"A\":1,\"B\":2}]}"); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Reject dangling edge references");
        string path = Path.Combine(output, "roundtrip.coat"); MainWindow.WriteAtomic(path, p.Serialize()); MainWindow.WriteAtomic(path, parsed.Serialize()); Check(File.ReadAllText(path) == p.Serialize(), "Atomic save and overwrite");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow { TestMode = true, ShowInTaskbar = false, Width = 1440, Height = 900 };
        window.Show(); window.UpdateLayout(); Pump();
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-empty.png"));
        var canvas = window.Canvas;
        Check(canvas.Tool == EditorTool.None, "Start with no active tool");
        window.SetTool(EditorTool.Draw);
        void Click(V2 point, bool ctrl = false)
        { canvas.PointerDown(canvas.Screen(point), MouseButton.Left, ctrl: ctrl); canvas.PointerUp(MouseButton.Left); }
        Click(new(0, 0)); Click(new(128, 0)); Click(new(128, 128)); Click(new(0, 128)); Click(new(0, 0));
        Check(window.Document.Edges.Count == 4 && window.Document.Faces().Count == 1 && canvas.Anchor == null, "Drawing gestures close a snapped contour");
        window.SetTool(EditorTool.Paint); canvas.PaintColor = "#A28555"; Click(new(64, 64));
        Check(window.Document.Fills.Values.Single() == "#A28555", "Paint gesture fills the clicked face");
        canvas.PaintLines = true; canvas.PaintColor = "#FF8800"; Click(new(64, 0));
        Check(window.Document.Edges.Single(e => e.Color == "#FF8800") != null, "Line paint gesture colors the hit edge");
        window.SetTool(EditorTool.Select); canvas.Mode = SelectionMode.Edge;
        canvas.PointerDown(canvas.Screen(new(64, 0)), MouseButton.Left, shift: true); canvas.PointerMove(canvas.Screen(new(64, -32))); canvas.PointerUp(MouseButton.Left);
        Check(window.Document.Faces().Count == 2 && window.Document.Edges.Count == 7, "Shift drag gesture extrudes the edge into a closed wall");
        window.Undo(); Check(window.Document.Edges.Count == 4, "Undo treats extrusion as one operation"); window.Redo(); Check(window.Document.Edges.Count == 7, "Redo restores gesture extrusion");
        canvas.Mode = SelectionMode.Vertex; canvas.ClearSelection();
        canvas.PointerDown(canvas.Screen(new(0, -32)), MouseButton.Left); canvas.PointerMove(canvas.Screen(new(-32, -32))); canvas.PointerUp(MouseButton.Left);
        Check(window.Document.Vertices.Any(v => v.P == new V2(-32, -32)), "Vertex drag moves shared geometry to a snapped position");
        string beforeCancel = window.Document.Serialize(); canvas.PointerDown(canvas.Screen(new(-32, -32)), MouseButton.Left); canvas.PointerMove(canvas.Screen(new(-64, -64))); canvas.CancelGesture();
        Check(window.Document.Serialize() == beforeCancel, "Escape cancels an in-progress geometry drag");
        canvas.Mode = SelectionMode.Edge; canvas.ClearSelection(); Click(new(64, 128)); Click(new(128, 64), ctrl: true);
        Check(canvas.SelectedEdges.Count == 2, "Ctrl click adds an edge to the selection");
        canvas.ClearSelection(); canvas.PointerDown(canvas.Screen(new(-96, 192)), MouseButton.Left); canvas.PointerMove(canvas.Screen(new(192, -96))); canvas.PointerUp(MouseButton.Left);
        Check(canvas.SelectedEdges.Count == window.Document.Edges.Count, "Marquee selects fully contained edges");
        canvas.ClearSelection(); canvas.Mode = SelectionMode.Face; Click(new(64, 64));
        Check(canvas.SelectionPoints().Count >= 4, "Face selection includes all contour points");
        var centerBefore = canvas.Center; canvas.PointerDown(new Point(300, 300), MouseButton.Middle); canvas.PointerMove(new Point(400, 350)); canvas.PointerUp(MouseButton.Middle);
        Check(canvas.Center != centerBefore, "Middle-button drag pans the view");
        window.SetTool(EditorTool.Draw); Click(new(256, 256)); canvas.PointerDown(canvas.Screen(new(256, 256)), MouseButton.Right); canvas.PointerUp(MouseButton.Right); Check(canvas.Anchor == null, "Right click ends the drawing chain");
        window.SetTool(EditorTool.Erase); var countBeforeErase = window.Document.Edges.Count; Click(new(64, 128)); Check(window.Document.Edges.Count == countBeforeErase - 1, "Eraser gesture removes the hit edge");
        void KeyPress(Key key) { window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); }
        KeyPress(Key.D1); Check(window.Document.Grid == 1, "Number row 1 selects unit grid"); KeyPress(Key.D0); Check(window.Document.Grid == 512, "Number row 0 selects 512-unit grid"); KeyPress(Key.NumPad6); Check(window.Document.Grid == 32, "Numpad 6 selects 32-unit grid");
        centerBefore = canvas.Center; KeyPress(Key.Right); Check(canvas.Center.X > centerBefore.X, "Arrow key pans the view");
        KeyPress(Key.D); Check(canvas.Tool == EditorTool.Draw, "D shortcut selects draw tool");
        window.LoadDocument(Plan.Demo(), ""); window.Canvas.Fit(); window.Canvas.ShowLengths = true; window.UpdateLayout(); Pump();
        MainWindow.WriteAtomic(Path.Combine(output, "Exemple.coat"), window.Document.Serialize());
        Check(window.Document.Faces().Count >= 5, "Example level contains selectable rooms");
        window.SetTool(EditorTool.Select); window.Canvas.Mode = SelectionMode.Edge; window.Canvas.SelectedEdges.Add(window.Document.Edges.First().Id); window.UpdateInspector(); window.UpdateLayout(); window.Canvas.InvalidateVisual(); Pump();
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-editor.png"));
        window.SetTool(EditorTool.Paint); window.UpdateLayout(); Pump(); MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-paint.png"));
        string start = window.Document.Serialize(); window.Mutate(() => window.Document.Segment(new(512, 0), new(768, 0))); window.Undo(); Check(window.Document.Serialize() == start, "Editor undo path restores document"); window.Redo(); Check(window.Document.Vertices.Any(v => v.X == 768), "Editor redo path restores segment");
        var bmp = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 255, 255, 255, 255 }, 8);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp)); using var memory = new MemoryStream(); encoder.Save(memory);
        window.Mutate(() => window.Document.Background = new ReferenceImage { Name = "test.png", Data = Convert.ToBase64String(memory.ToArray()), Width = 128, Height = 128 });
        var imagePlan = Plan.Parse(window.Document.Serialize()); Check(EditorCanvas.DecodeImage(imagePlan.Background!.Data).PixelWidth == 2, "Background image is embedded and reloadable");
        var svg = MainWindow.Svg(imagePlan); Check(svg.Contains("data:image/png;base64,") && svg.Contains("<line ") && svg.Contains("<polygon "), "SVG export embeds image, geometry and fills"); File.WriteAllText(Path.Combine(output, "export.svg"), svg);
        window.Canvas.Exporting = true; window.Canvas.InvalidateVisual(); Pump(); MainWindow.SaveVisual(window.Canvas, Path.Combine(output, "export.png"), 2); window.Canvas.Exporting = false;
        Check(new FileInfo(Path.Combine(output, "export.png")).Length > 1000, "PNG export renders a valid image");
        window.Document.Background = null; window.Canvas.RefreshGeometry(); window.SetTool(EditorTool.Select); window.Width = 1050; window.Height = 680; window.Canvas.Fit(); window.UpdateLayout(); Pump(); MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-compact.png"));
        RunUpdateTests(window, Check, output);
        Update03Tests.Run(window, Check, output);
        Update04Tests.Run(window, Check, output);
        Update05Tests.Run(window, Check, output);
        Update06Tests.Run(window, Check, output);
        Update07Tests.Run(window, Check, output);
        Update08Tests.Run(window, Check, output);
        Update10Tests.Run(window, Check, output);
        Update11Tests.Run(window, Check, output);
        Update12Tests.Run(window, Check, output);
        Update13Tests.Run(Check, output);
        Update105Tests.Run(window, Check, output);
        InternationalTests.Run(window, Check, output);
        window.Close(); app.Shutdown();
        File.WriteAllLines(Path.Combine(output, "results.txt"), results.Append($"\n{results.Count} checks passed."));
    }
    private static void RunUpdateTests(MainWindow window, Action<bool, string> check, string output)
    {
        void Rectangle(Plan plan, int level, double x, double y, double w = 128, double h = 128, string color = "#66C5AD")
        {
            plan.ActiveLevel = level; plan.EnsureLevel(level);
            var corners = new[] { new V2(x, y), new V2(x + w, y), new V2(x + w, y + h), new V2(x, y + h) };
            for (int i = 0; i < 4; i++) plan.Segment(corners[i], corners[(i + 1) % 4]);
            plan.Fills[plan.HitFace(new V2(x + w / 2, y + h / 2))!.Key] = color;
        }
        var plan = new Plan(); Rectangle(plan, 0, 0, 0); Rectangle(plan, 1, 0, 0, color: "#D38D9E");
        check(plan.Vertices.Count == 8 && plan.Edges.Count == 8 && plan.Faces().Count == 2, "Coincident rooms on separate levels retain independent geometry");
        plan.Normalize(); check(plan.Fills.Count == 2 && plan.Fills.Values.Distinct().Count() == 2, "Normalization never merges fills across levels");
        var divided = Plan.Parse(plan.Serialize()); divided.ActiveLevel = 0; divided.Segment(new(64, 0), new(64, 128));
        check(divided.Faces().Count(f => f.Level == 0) == 2 && divided.Faces().Where(f => f.Level == 0).All(f => divided.Fills[f.Key] == "#66C5AD") && divided.Fills[divided.Faces().Single(f => f.Level == 1).Key] == "#D38D9E", "Partitioning an overlapped floor retains only that floor's colors");
        plan.ActiveLevel = 0; check(plan.HitFace(new(64, 64))!.Level == 0, "Model face picking respects active level");
        var cross = new Plan(); cross.Segment(new(0, 0), new(128, 128)); cross.ActiveLevel = 1; cross.Segment(new(0, 128), new(128, 0));
        check(cross.Vertices.Count == 4 && cross.Edges.Count == 2, "Crossings on different levels do not create intersections");
        int crossSource = cross.Edges.First(e => cross.LevelOf(e) == 0).Id; var crossTransfer = cross.TransferToLevel(Array.Empty<int>(), new[] { crossSource }, Array.Empty<string>(), 1);
        check(cross.Edges.Count == 4 && crossTransfer.Edges.Count == 2, "A transferred edge split by a target-level crossing keeps both pieces selected");
        var sourceFace = plan.Faces().Single(f => f.Level == 0); var sourceColor = plan.Fills[sourceFace.Key];
        var transfer = plan.TransferToLevel(Array.Empty<int>(), Array.Empty<int>(), new[] { sourceFace.Key }, 2);
        check(plan.Vertices.All(v => v.Level != 0) && transfer.Faces.Count == 1 && plan.Fills[transfer.Faces.Single()] == sourceColor, "Moving a filled face transfers its contour and color to a new level");
        check(plan.Faces().Count == 2 && plan.Fills.Count == 2 && plan.Vertices.Count(v => v.Level == 1) == 4, "Transferring a face preserves other levels");
        plan = new Plan(); Rectangle(plan, 0, 0, 0); var oldEdge = plan.Edges.First(); var otherIds = plan.Edges.Skip(1).Select(e => e.Id).ToHashSet();
        transfer = plan.TransferToLevel(Array.Empty<int>(), new[] { oldEdge.Id }, Array.Empty<string>(), 1);
        check(plan.Edges.Count(e => plan.LevelOf(e) == 0) == 3 && plan.Edges.Count(e => plan.LevelOf(e) == 1) == 1 && plan.Edges.Where(e => otherIds.Contains(e.Id)).All(e => plan.LevelOf(e) == 0), "Moving a shared edge detaches its endpoints without moving neighbouring edges");
        check(plan.Edges.All(e => plan.Get(e.A).Level == plan.Get(e.B).Level), "Every edge remains wholly on one level after transfer");
        plan = new Plan(); Rectangle(plan, 0, 0, 0); int vertexId = plan.Vertices.First().Id; transfer = plan.TransferToLevel(new[] { vertexId }, Array.Empty<int>(), Array.Empty<string>(), -1);
        check(plan.Edges.Count(e => plan.LevelOf(e) == -1) == 2 && plan.Edges.Count(e => plan.LevelOf(e) == 0) == 2 && transfer.Points.Count == 1, "Point-level assignment carries incident edges and preserves the others");
        plan = new Plan(); Rectangle(plan, 0, 0, 0); Rectangle(plan, 1, 0, 0, color: "#D38D9E"); sourceFace = plan.Faces().Single(f => f.Level == 0);
        plan.TransferToLevel(Array.Empty<int>(), Array.Empty<int>(), new[] { sourceFace.Key }, 1);
        check(plan.Faces().Count == 1 && plan.Edges.Count == 4 && plan.Fills.Values.Single() == "#66C5AD", "Transferring onto an identical target contour merges cleanly and retains the assigned color");
        plan.ActiveLevel = 1; var roundtrip = Plan.Parse(plan.Serialize());
        check(roundtrip.Serialize() == plan.Serialize() && roundtrip.ActiveLevel == 1, "Save and reload retain levels, active level and fills");
        var legacy = new Plan(); Rectangle(legacy, 0, 0, 0); legacy.Version = 1; var migrated = Plan.Parse(legacy.Serialize());
        check(migrated.Version == 6 && migrated.Vertices.All(v => v.Level == 0) && migrated.Fills.Count == 1, "Version 0.1 projects migrate to level zero without losing fills");
        bool rejected = false; var invalid = new Plan(); invalid.Segment(new(0, 0), new(32, 0)); invalid.Vertices[0].Level = 1;
        try { Plan.Parse(invalid.Serialize()); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "Reject files with edges joining different levels");

        var canvas = window.Canvas; window.Width = 1440; window.Height = 900; window.UpdateLayout();
        void Click(V2 p) { canvas.PointerDown(canvas.Screen(p), MouseButton.Left); canvas.PointerUp(MouseButton.Left); }
        void KeyPress(Key key) { window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); }
        string Texts(DependencyObject parent)
        {
            var s = parent is System.Windows.Controls.TextBlock t ? t.Text + "\n" : "";
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) s += Texts(VisualTreeHelper.GetChild(parent, i));
            return s;
        }
        IEnumerable<LevelPreview> Previews(DependencyObject parent)
        {
            if (parent is LevelPreview p) yield return p;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) foreach (var preview in Previews(VisualTreeHelper.GetChild(parent, i))) yield return preview;
        }
        window.LoadDocument(new Plan(), ""); window.SetTool(EditorTool.None); window.UpdateLayout(); Pump();
        var text = Texts((DependencyObject)window.Content);
        check(text.Contains("NIVEAUX") && text.Contains("IMAGE DE RÉFÉRENCE") && !text.Contains("REPÈRES") && !text.Contains("DOCUMENT"), "Idle inspector contains levels and global settings, with no Document section");
        var noToolState = window.Document.Serialize(); Click(new(0, 0)); Click(new(128, 128));
        check(window.Document.Serialize() == noToolState && !canvas.HasSelectionOrGesture, "Clicking without an active tool never draws or selects");
        canvas.ShowGrid = false; canvas.InvalidateVisual(); Pump();
        byte[] PixelAt(V2 point)
        {
            var bmp = new RenderTargetBitmap((int)canvas.ActualWidth, (int)canvas.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(canvas);
            var screen = canvas.Screen(point); var pixel = new byte[4]; bmp.CopyPixels(new Int32Rect((int)screen.X, (int)screen.Y, 1, 1), pixel, 4, 0); return pixel;
        }
        var emptyPixel = PixelAt(new(0, 0)); check(emptyPixel[0] == 42 && emptyPixel[1] == 31 && emptyPixel[2] == 24, "Empty canvas renders only its background at the center, without a welcome message");
        canvas.ShowGrid = true;
        foreach (var tool in new[] { EditorTool.Select, EditorTool.Draw, EditorTool.Erase, EditorTool.Paint })
        {
            window.SetTool(tool); window.UpdateLayout(); Pump(); text = Texts((DependencyObject)window.Content);
            check(text.Contains("NIVEAUX") && !text.Contains("OUTIL ACTIF") && !text.Contains("IMAGE DE RÉFÉRENCE") && !text.Contains("REPÈRES") && !text.Contains("DOCUMENT"), $"Inspector for {tool} shows only levels and tool properties");
        }
        window.SetTool(EditorTool.Draw); Click(new(0, 0)); KeyPress(Key.Escape);
        check(canvas.Tool == EditorTool.Select && canvas.Anchor == null, "Escape cancels a pending point and returns to Select");
        KeyPress(Key.Escape); check(canvas.Tool == EditorTool.Select, "Repeated Escape keeps Select active");
        window.SetTool(EditorTool.Draw); Click(new(0, 0)); Click(new(128, 0)); canvas.CancelGesture(); window.SetTool(EditorTool.Select); canvas.Mode = SelectionMode.Edge; Click(new(64, 0)); KeyPress(Key.Escape);
        check(canvas.Tool == EditorTool.Select && canvas.SelectionPoints().Count == 0, "First Escape clears an edge selection and retains the selection tool");
        KeyPress(Key.Escape); check(canvas.Tool == EditorTool.Select, "Repeated Escape never deactivates Select");
        window.SetTool(EditorTool.None); KeyPress(Key.Escape); check(canvas.Tool == EditorTool.Select, "Escape activates Select from the idle state");

        var layered = new Plan(); Rectangle(layered, -1, -256, 0); Rectangle(layered, 0, 0, 0); Rectangle(layered, 1, 256, 0); layered.ActiveLevel = 0;
        window.LoadDocument(layered, ""); window.SetTool(EditorTool.Select); canvas.Center = new(64, 64); canvas.Zoom = 1; canvas.Mode = SelectionMode.Vertex;
        Click(new(-256, 0)); check(canvas.SelectionPoints().Count == 0, "Lower-level ghost points cannot be selected");
        Click(new(256, 0)); check(canvas.SelectionPoints().Count == 0, "Hidden higher-level points cannot be selected");
        Click(new(0, 0)); check(canvas.SelectedVertices.Count == 1, "Current-level points remain selectable");
        canvas.Mode = SelectionMode.Edge; canvas.ClearSelection(); canvas.SelectAll(); check(canvas.SelectedEdges.Count == 4 && canvas.SelectedEdges.All(id => layered.LevelOf(layered.Edges.First(e => e.Id == id)) == 0), "Select all is restricted to active-level edges");
        canvas.ClearSelection(); canvas.PointerDown(canvas.Screen(new(-300, 200)), MouseButton.Left); canvas.PointerMove(canvas.Screen(new(450, -64))); canvas.PointerUp(MouseButton.Left);
        check(canvas.SelectedEdges.Count == 4, "Marquee ignores lower and higher levels");
        canvas.ClearSelection(); window.SetTool(EditorTool.Paint); canvas.PaintLines = false; canvas.PaintColor = "#FFFFFF"; string protectedState = layered.Serialize(); Click(new(-192, 64)); Click(new(320, 64));
        check(layered.Serialize() == protectedState, "Paint cannot modify inactive levels");
        window.SetTool(EditorTool.Erase); Click(new(-192, 0)); Click(new(320, 0)); check(layered.Serialize() == protectedState, "Eraser cannot modify inactive levels");
        canvas.ShowGrid = false; canvas.ShowLengths = false; canvas.Exporting = true; canvas.InvalidateVisual(); Pump();
        var lower = PixelAt(new(-192, 64)); var current = PixelAt(new(64, 64)); var upper = PixelAt(new(320, 64));
        check(current[1] > lower[1] && lower[1] > upper[1] && upper[1] == 31, "Rendered current fill is opaque relative to a faded lower fill; upper fill is absent");
        var lowerEdge = PixelAt(new(-192, 0)); var currentEdge = PixelAt(new(64, 0)); var upperEdge = PixelAt(new(320, 0));
        check(currentEdge[1] > lowerEdge[1] && lowerEdge[1] > upperEdge[1] && upperEdge[1] == 31, "Rendered edges fade below the active level and disappear above it");
        canvas.Exporting = false; canvas.ShowGrid = true;
        var svg = MainWindow.Svg(layered); var xml = System.Xml.Linq.XDocument.Parse(svg); var ns = xml.Root!.Name.Namespace;
        var groups = xml.Root.Elements(ns + "g").ToList(); check(groups.Count == 2 && groups.Any(g => (string?)g.Attribute("data-level") == "-1" && (string?)g.Attribute("opacity") == "0.6") && groups.All(g => (string?)g.Attribute("data-level") != "1"), "SVG export follows level visibility and opacity");
        window.SetTool(EditorTool.Select); canvas.Mode = SelectionMode.Face; Click(new(64, 64)); string beforeAssign = layered.Serialize(); window.AssignSelectionToLevel(2);
        check(window.Document.ActiveLevel == 2 && window.Document.Faces().All(f => f.Level != 0) && canvas.SelectedFaces.Count == 1 && window.Document.Fills[canvas.SelectedFaces.Single()] == "#66C5AD", "Inspector assignment moves a selected colored zone and activates its destination");
        window.Undo(); check(window.Document.Serialize() == beforeAssign, "Undo restores both geometry and active level after assignment");
        window.Redo(); check(window.Document.ActiveLevel == 2 && window.Document.Faces().Any(f => f.Level == 2), "Redo restores a level assignment");
        KeyPress(Key.PageDown); check(window.Document.ActiveLevel == 1 && canvas.SelectionPoints().Count == 0, "Changing level clears selection and updates the active plane");
        KeyPress(Key.PageUp); check(window.Document.ActiveLevel == 2, "Page Up activates the next level");
        window.ChangeLevel(-2); window.SetTool(EditorTool.Draw); Click(new(0, 0)); Click(new(128, 0)); check(window.Document.Edges.Count(e => window.Document.LevelOf(e) == -2) == 1, "New segments are created on the active level, including negative levels");
        window.SetTool(EditorTool.Select); canvas.Mode = SelectionMode.Edge; Click(new(64, 0)); window.Duplicate(); check(window.Document.Edges.Count(e => window.Document.LevelOf(e) == -2) == 2, "Duplication remains on the active level");
        window.Undo(); canvas.SelectedEdges.Add(window.Document.Edges.Single(e => window.Document.LevelOf(e) == -2).Id); canvas.PointerDown(canvas.Screen(new(64, 0)), MouseButton.Left, shift: true); canvas.PointerMove(canvas.Screen(new(64, -32))); canvas.PointerUp(MouseButton.Left);
        check(window.Document.Faces().Any(f => f.Level == -2 && f.Area == 4096), "Extrusion creates its wall on the original active level");

        var demo = Plan.Demo(); Rectangle(demo, 1, -96, -128, 320, 256, "#66C5AD"); Rectangle(demo, 2, 0, -64, 128, 128, "#D38D9E"); demo.ActiveLevel = 1;
        window.LoadDocument(demo, ""); window.SetTool(EditorTool.None); canvas.Fit(); window.UpdateLayout(); Pump();
        check(Previews((DependencyObject)window.Content).Single().IsHitTestVisible == false && !Previews((DependencyObject)window.Content).Single().Focusable, "Three-quarter level preview cannot receive mouse or keyboard input");
        var preview = Previews((DependencyObject)window.Content).Single(); var previewBitmap = new RenderTargetBitmap((int)preview.ActualWidth, (int)preview.ActualHeight, 96, 96, PixelFormats.Pbgra32); previewBitmap.Render(preview);
        var pixels = new byte[previewBitmap.PixelWidth * previewBitmap.PixelHeight * 4]; previewBitmap.CopyPixels(pixels, previewBitmap.PixelWidth * 4, 0);
        int reds = 0, grays = 0, blacks = 0; for (int i = 0; i < pixels.Length; i += 4) { if (pixels[i + 2] > 100 && pixels[i + 2] > pixels[i + 1] * 1.7) reds++; if (pixels[i] > 40 && Math.Abs(pixels[i] - pixels[i + 2]) < 20) grays++; if (pixels[i] + pixels[i + 1] + pixels[i + 2] < 10) blacks++; }
        check(reds > 100 && grays > 100 && blacks > 1000, "Level preview renders red active plane, gray other planes and black background");
        MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-levels-idle.png"));
        MainWindow.WriteAtomic(Path.Combine(output, "Exemple-niveaux.coat"), demo.Serialize());
        window.SetTool(EditorTool.Select); canvas.Mode = SelectionMode.Face; Click(new(32, 0)); window.UpdateLayout(); Pump(); MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-levels-selection.png"));
        window.Width = 1050; window.Height = 680; window.UpdateLayout(); canvas.Fit(); Pump(); MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-levels-compact.png"));
        window.Width = 1440; window.Height = 900; window.LoadDocument(new Plan(), ""); window.SetTool(EditorTool.None); window.UpdateLayout(); Pump(); MainWindow.SaveVisual((FrameworkElement)window.Content, Path.Combine(output, "coat-empty.png"));
    }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
}
