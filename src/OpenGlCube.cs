// Coat — Epicteta Lupercal
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Wpf;

namespace Coat;

internal sealed class OpenGlCube : IDisposable
{
    private readonly GLWpfControl control = new() { Focusable = false, IsHitTestVisible = false };
    private readonly PerspectiveCamera camera;
    private readonly MeshGeometry3D mesh;
    private readonly Viewport3D fallback;
    private readonly Grid parent;
    private int program, vao, vertices, cameraUniform;
    internal int Frames { get; private set; }

    internal OpenGlCube(Grid parent, Viewport3D fallback, PerspectiveCamera camera, MeshGeometry3D mesh)
    {
        this.parent = parent;
        this.fallback = fallback;
        this.camera = camera;
        this.mesh = mesh;
        try
        {
            control.Start(new GLWpfControlSettings { MajorVersion = 3, MinorVersion = 3, RenderContinuously = false });
            control.Render += Draw;
            parent.Children.Add(control);
            fallback.Visibility = Visibility.Hidden;
            camera.Changed += CameraChanged;
        }
        catch (Exception e) { Recover(e); }
    }

    private void CameraChanged(object? sender, EventArgs e) => control.InvalidateVisual();

    private void Draw(TimeSpan delta)
    {
        try
        {
            if (program == 0) Upload();
            int width = control.FrameBufferWidth, height = control.FrameBufferHeight;
            float aspect = width / (float)height;
            float vertical = 2 * MathF.Atan(MathF.Tan((float)(camera.FieldOfView * Math.PI / 360)) / aspect);
            var position = camera.Position;
            var view = Matrix4.LookAt(new Vector3((float)position.X, (float)position.Y, (float)position.Z), Vector3.Zero, Vector3.UnitY);
            var projection = view * Matrix4.CreatePerspectiveFieldOfView(vertical, aspect, (float)camera.NearPlaneDistance, (float)camera.FarPlaneDistance);
            GL.Disable(EnableCap.ScissorTest);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Less);
            GL.ClearColor(38/255f,42/255f,47/255f,1);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.UseProgram(program);
            GL.UniformMatrix4(cameraUniform, false, ref projection);
            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, mesh.TriangleIndices.Count);
            GL.BindVertexArray(0);
            GL.Disable(EnableCap.DepthTest);
            Frames++;
        }
        catch (Exception e) { parent.Dispatcher.BeginInvoke(new Action(() => Recover(e))); }
    }

    private void Upload()
    {
        program = GL.CreateProgram();
        Attach(ShaderType.VertexShader, """
            #version 330 core
            layout(location=0) in vec3 pos;
            layout(location=1) in vec3 normal;
            uniform mat4 viewProjection;
            out vec3 shade;
            void main() {
                float light = 0.44 + 0.48 * max(0.0, dot(normal, normalize(vec3(1,1.6,0.75))))
                                   + 0.22 * max(0.0, dot(normal, normalize(vec3(-0.7,-0.2,-1))));
                shade = vec3(0.65,0.69,0.72) * light;
                gl_Position = viewProjection * vec4(pos,1);
            }
            """);
        Attach(ShaderType.FragmentShader, """
            #version 330 core
            in vec3 shade;
            out vec4 color;
            void main() { color = vec4(shade,1); }
            """);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linked);
        if (linked == 0) throw new InvalidOperationException(GL.GetProgramInfoLog(program));
        cameraUniform = GL.GetUniformLocation(program, "viewProjection");
        var data = new List<float>(mesh.TriangleIndices.Count * 6);
        foreach (int index in mesh.TriangleIndices)
        {
            var p = mesh.Positions[index]; var n = mesh.Normals[index];
            data.AddRange([(float)p.X,(float)p.Y,(float)p.Z,(float)n.X,(float)n.Y,(float)n.Z]);
        }
        vao = GL.GenVertexArray(); GL.BindVertexArray(vao);
        vertices = GL.GenBuffer(); GL.BindBuffer(BufferTarget.ArrayBuffer, vertices);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Count * sizeof(float), data.ToArray(), BufferUsageHint.StaticDraw);
        for (int i = 0; i < 2; i++)
        {
            GL.EnableVertexAttribArray(i);
            GL.VertexAttribPointer(i, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), i * 3 * sizeof(float));
        }
    }

    private void Attach(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        try
        {
            GL.ShaderSource(shader, source); GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0) throw new InvalidOperationException(GL.GetShaderInfoLog(shader));
            GL.AttachShader(program, shader);
        }
        finally { GL.DeleteShader(shader); }
    }

    private void Recover(Exception e)
    {
        System.Diagnostics.Trace.WriteLine($"Coat OpenGL cube: {e}");
        Dispose();
        fallback.Visibility = Visibility.Visible;
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        camera.Changed -= CameraChanged;
        control.Render -= Draw;
        if (program != 0)
        {
            control.Context?.MakeCurrent();
            GL.DeleteBuffer(vertices); GL.DeleteVertexArray(vao); GL.DeleteProgram(program);
        }
        parent.Children.Remove(control);
        control.Dispose();
    }
}
