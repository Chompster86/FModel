using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using OpenTK.Graphics.OpenGL4;

namespace FModel.Views.Snooper.Shading;

public class Shader : IDisposable
{
    private readonly int _handle;
    private readonly int _vHandle;
    private readonly int _fHandle;
    private readonly Dictionary<string, int> _uniformsLocation = new ();

    public Shader() : this("default") {}

    public Shader(string name1, string name2 = null)
    {
        _handle = GL.CreateProgram();
        _vHandle = LoadShader(ShaderType.VertexShader, $"{name1}.vert");
        _fHandle = LoadShader(ShaderType.FragmentShader, $"{name2 ?? name1}.frag");
        Attach();
    }

    private void Attach()
    {
        GL.AttachShader(_handle, _vHandle);
        GL.AttachShader(_handle, _fHandle);
        GL.LinkProgram(_handle);
        GL.GetProgram(_handle, GetProgramParameterName.LinkStatus, out var status);
        if (status == 0)
        {
            throw new Exception($"Program failed to link with error: {GL.GetProgramInfoLog(_handle)}");
        }
    }

    private void Detach()
    {
        GL.DetachShader(_handle, _vHandle);
        GL.DetachShader(_handle, _fHandle);
        GL.DeleteShader(_vHandle);
        GL.DeleteShader(_fHandle);
    }

    private int LoadShader(ShaderType type, string file)
    {
        var executingAssembly = Assembly.GetExecutingAssembly();
        var executingAssemblyName = executingAssembly.GetName().Name;
        using var stream = executingAssembly.GetManifestResourceStream($"{executingAssemblyName}.Resources.{file}");
        using var reader = new StreamReader(stream);
        var handle = GL.CreateShader(type);

        var content = reader.ReadToEnd();
        if (file.Equals("default.frag") && GL.GetInteger(GetPName.MaxTextureCoords) == 0)
            content = content.Replace("#define MAX_UV_COUNT 8", "#define MAX_UV_COUNT 1");
        if (type == ShaderType.VertexShader && Array.IndexOf(["default.vert", "outline.vert", "picking.vert"], file) > -1)
        {
            using var splineStream = executingAssembly.GetManifestResourceStream($"{executingAssemblyName}.Resources.spline.vert");
            using var splineReader = new StreamReader(splineStream);
            content = splineReader.ReadToEnd() + Environment.NewLine + content.Replace("#version 460 core", "");
        }

        GL.ShaderSource(handle, content);
        GL.CompileShader(handle);
        string infoLog = GL.GetShaderInfoLog(handle);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            throw new Exception($"Error compiling shader of type {type}, failed with error {infoLog}");
        }

        return handle;
    }

    public void Use()
    {
        GL.UseProgram(_handle);
    }

    public void Render(Matrix4x4 viewMatrix, Vector3 viewPos, Matrix4x4 projMatrix)
    {
        Render(viewMatrix, projMatrix);
        SetUniform("uViewPos", viewPos);
    }
    public void Render(Matrix4x4 viewMatrix, Matrix4x4 projMatrix)
    {
        Use();
        SetUniform("uView", viewMatrix);
        SetUniform("uProjection", projMatrix);
    }

    public void SetUniform(string name, int value)
    {
        GL.Uniform1(GetUniformLocation(name), value);
    }

    public unsafe void SetUniform(string name, Matrix4x4 value) => UniformMatrix4(name, (float*) &value);
    public unsafe void UniformMatrix4(string name, float* value)
    {
        GL.UniformMatrix4(GetUniformLocation(name), 1, false, value);
    }

    public void SetUniform(string name, bool value) => SetUniform(name, Convert.ToUInt32(value));

    public void SetUniform(string name, uint value)
    {
        GL.Uniform1(GetUniformLocation(name), value);
    }

    public void SetUniform(string name, float value)
    {
        GL.Uniform1(GetUniformLocation(name), value);
    }

    public void SetUniform(string name, Vector2 value) => SetUniform3(name, value.X, value.Y);
    public void SetUniform3(string name, float x, float y)
    {
        GL.Uniform2(GetUniformLocation(name), x, y);
    }

    public void SetUniform(string name, Vector3 value) => SetUniform3(name, value.X, value.Y, value.Z);
    public void SetUniform3(string name, float x, float y, float z)
    {
        GL.Uniform3(GetUniformLocation(name), x, y, z);
    }

    public void SetUniform(string name, Vector4 value) => SetUniform4(name, value.X, value.Y, value.Z, value.W);
    public void SetUniform4(string name, float x, float y, float z, float w)
    {
        GL.Uniform4(GetUniformLocation(name), x, y, z, w);
    }

    private int GetUniformLocation(string name)
    {
        if (!_uniformsLocation.TryGetValue(name, out int location))
        {
            location = GL.GetUniformLocation(_handle, name);
            _uniformsLocation.Add(name, location);
            if (location == -1)
            {
                throw new Exception($"{name} uniform not found on shader.");
            }
        }
        return location;
    }

    public void Dispose()
    {
        Detach();
        GL.DeleteProgram(_handle);
    }
}
