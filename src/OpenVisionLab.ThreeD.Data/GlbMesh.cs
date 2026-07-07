using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace OpenVisionLab.ThreeD.Data;

public sealed class ImportedMesh
{
    private ImportedMesh(
        string sourcePath,
        string name,
        string format,
        Vector3[] positions,
        int[] indices,
        Vector4[] vertexColors,
        Vector2[] textureCoordinates,
        GlbTextureImage? baseColorTexture)
    {
        SourcePath = sourcePath;
        Name = name;
        Format = format;
        Positions = positions;
        Indices = indices;
        VertexColors = vertexColors;
        TextureCoordinates = textureCoordinates;
        BaseColorTexture = baseColorTexture;
        TriangleCount = indices.Length / 3;

        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var position in positions)
        {
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        Min = min;
        Max = max;
    }

    public string SourcePath { get; }

    public string Name { get; }

    public string Format { get; }

    public Vector3[] Positions { get; }

    public int[] Indices { get; }

    public Vector4[] VertexColors { get; }

    public bool HasVertexColors => VertexColors.Length == Positions.Length;

    public Vector2[] TextureCoordinates { get; }

    public GlbTextureImage? BaseColorTexture { get; }

    public bool HasBaseColorTexture => BaseColorTexture is not null && TextureCoordinates.Length == Positions.Length;

    public int TriangleCount { get; }

    public Vector3 Min { get; }

    public Vector3 Max { get; }

    public static ImportedMesh Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 20 || Encoding.ASCII.GetString(bytes, 0, 4) != "glTF")
        {
            throw new InvalidDataException($"Unsupported GLB header: {path}");
        }

        var version = ReadUInt32(bytes, 4);
        var declaredLength = ReadUInt32(bytes, 8);
        if (version != 2 || declaredLength != bytes.Length)
        {
            throw new InvalidDataException($"Unsupported GLB 2.0 length/version: {path}");
        }

        var offset = 12;
        JsonDocument? document = null;
        ReadOnlyMemory<byte> binaryChunk = ReadOnlyMemory<byte>.Empty;

        while (offset + 8 <= bytes.Length)
        {
            var chunkLength = checked((int)ReadUInt32(bytes, offset));
            var chunkType = ReadUInt32(bytes, offset + 4);
            offset += 8;
            if (offset + chunkLength > bytes.Length)
            {
                throw new InvalidDataException($"GLB chunk exceeds file length: {path}");
            }

            if (chunkType == 0x4E4F534A)
            {
                var json = Encoding.UTF8.GetString(bytes, offset, chunkLength);
                document = JsonDocument.Parse(json);
            }
            else if (chunkType == 0x004E4942)
            {
                binaryChunk = new ReadOnlyMemory<byte>(bytes, offset, chunkLength);
            }

            offset += Pad4(chunkLength);
        }

        using (document)
        {
            if (document is null || binaryChunk.IsEmpty)
            {
                throw new InvalidDataException($"GLB must contain JSON and BIN chunks: {path}");
            }

            var root = document.RootElement;
            var primitive = root.GetProperty("meshes")[0].GetProperty("primitives")[0];
            var mode = primitive.TryGetProperty("mode", out var modeElement) ? modeElement.GetInt32() : 4;
            if (mode != 4)
            {
                throw new InvalidDataException($"Only GLB triangle primitives are supported: {path}");
            }

            var meshName = root.GetProperty("meshes")[0].TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? Path.GetFileNameWithoutExtension(path)
                : Path.GetFileNameWithoutExtension(path);
            var attributes = primitive.GetProperty("attributes");
            var positionAccessorIndex = attributes.GetProperty("POSITION").GetInt32();
            var positions = ReadVec3Accessor(root, binaryChunk.Span, positionAccessorIndex);
            var indices = primitive.TryGetProperty("indices", out var indexElement)
                ? ReadIndexAccessor(root, binaryChunk.Span, indexElement.GetInt32())
                : Enumerable.Range(0, positions.Length).ToArray();
            var vertexColors = attributes.TryGetProperty("COLOR_0", out var colorElement)
                ? ReadColorAccessor(root, binaryChunk.Span, colorElement.GetInt32())
                : Array.Empty<Vector4>();
            var textureCoordinates = attributes.TryGetProperty("TEXCOORD_0", out var texCoordElement)
                ? ReadVec2Accessor(root, binaryChunk.Span, texCoordElement.GetInt32())
                : Array.Empty<Vector2>();
            var texture = ReadBaseColorTexture(root, binaryChunk, primitive);

            if (positions.Length == 0 || indices.Length == 0 || indices.Length % 3 != 0)
            {
                throw new InvalidDataException($"GLB mesh has no complete triangles: {path}");
            }

            if (vertexColors.Length > 0 && vertexColors.Length != positions.Length)
            {
                throw new InvalidDataException($"GLB COLOR_0 count must match POSITION count: {path}");
            }

            if (textureCoordinates.Length > 0 && textureCoordinates.Length != positions.Length)
            {
                throw new InvalidDataException($"GLB TEXCOORD_0 count must match POSITION count: {path}");
            }

            return new ImportedMesh(path, meshName, "GLB", positions, indices, vertexColors, textureCoordinates, texture);
        }
    }

    public static ImportedMesh CreateTriangleMesh(string path, string name, string format, Vector3[] positions, int[] indices)
    {
        if (positions.Length == 0)
        {
            throw new InvalidDataException($"{format} mesh has no vertices: {path}");
        }

        if (indices.Length == 0 || indices.Length % 3 != 0)
        {
            throw new InvalidDataException($"{format} mesh has no complete triangles: {path}");
        }

        foreach (var index in indices)
        {
            if (index < 0 || index >= positions.Length)
            {
                throw new InvalidDataException($"{format} mesh index is outside the vertex range: {path}");
            }
        }

        return new ImportedMesh(path, name, format, positions, indices, [], [], null);
    }

    private static Vector3[] ReadVec3Accessor(JsonElement root, ReadOnlySpan<byte> binary, int accessorIndex)
    {
        var accessor = root.GetProperty("accessors")[accessorIndex];
        if (accessor.GetProperty("componentType").GetInt32() != 5126
            || accessor.GetProperty("type").GetString() != "VEC3")
        {
            throw new InvalidDataException("Only float VEC3 accessors are supported for GLB positions.");
        }

        var count = accessor.GetProperty("count").GetInt32();
        var (start, stride) = GetAccessorLayout(root, accessor);
        var positions = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            var itemOffset = start + i * stride;
            positions[i] = new Vector3(
                ReadSingle(binary, itemOffset),
                ReadSingle(binary, itemOffset + 4),
                ReadSingle(binary, itemOffset + 8));
        }

        return positions;
    }

    private static int[] ReadIndexAccessor(JsonElement root, ReadOnlySpan<byte> binary, int accessorIndex)
    {
        var accessor = root.GetProperty("accessors")[accessorIndex];
        if (accessor.GetProperty("type").GetString() != "SCALAR")
        {
            throw new InvalidDataException("Only scalar GLB index accessors are supported.");
        }

        var componentType = accessor.GetProperty("componentType").GetInt32();
        var count = accessor.GetProperty("count").GetInt32();
        var (start, stride) = GetAccessorLayout(root, accessor);
        var indices = new int[count];
        for (var i = 0; i < count; i++)
        {
            var itemOffset = start + i * stride;
            indices[i] = componentType switch
            {
                5121 => binary[itemOffset],
                5123 => BitConverter.ToUInt16(binary.Slice(itemOffset, 2)),
                5125 => checked((int)BitConverter.ToUInt32(binary.Slice(itemOffset, 4))),
                _ => throw new InvalidDataException($"Unsupported GLB index component type: {componentType}")
            };
        }

        return indices;
    }

    private static Vector2[] ReadVec2Accessor(JsonElement root, ReadOnlySpan<byte> binary, int accessorIndex)
    {
        var accessor = root.GetProperty("accessors")[accessorIndex];
        if (accessor.GetProperty("componentType").GetInt32() != 5126
            || accessor.GetProperty("type").GetString() != "VEC2")
        {
            throw new InvalidDataException("Only float VEC2 accessors are supported for GLB texture coordinates.");
        }

        var count = accessor.GetProperty("count").GetInt32();
        var (start, stride) = GetAccessorLayout(root, accessor);
        var textureCoordinates = new Vector2[count];
        for (var i = 0; i < count; i++)
        {
            var itemOffset = start + i * stride;
            textureCoordinates[i] = new Vector2(
                ReadSingle(binary, itemOffset),
                ReadSingle(binary, itemOffset + 4));
        }

        return textureCoordinates;
    }

    private static Vector4[] ReadColorAccessor(JsonElement root, ReadOnlySpan<byte> binary, int accessorIndex)
    {
        var accessor = root.GetProperty("accessors")[accessorIndex];
        if (accessor.GetProperty("componentType").GetInt32() != 5126)
        {
            throw new InvalidDataException("Only float GLB COLOR_0 accessors are supported.");
        }

        var type = accessor.GetProperty("type").GetString();
        if (type is not ("VEC3" or "VEC4"))
        {
            throw new InvalidDataException("Only VEC3 or VEC4 GLB COLOR_0 accessors are supported.");
        }

        var count = accessor.GetProperty("count").GetInt32();
        var (start, stride) = GetAccessorLayout(root, accessor);
        var colors = new Vector4[count];
        for (var i = 0; i < count; i++)
        {
            var itemOffset = start + i * stride;
            colors[i] = new Vector4(
                ReadSingle(binary, itemOffset),
                ReadSingle(binary, itemOffset + 4),
                ReadSingle(binary, itemOffset + 8),
                type == "VEC4" ? ReadSingle(binary, itemOffset + 12) : 1.0f);
        }

        return colors;
    }

    private static GlbTextureImage? ReadBaseColorTexture(JsonElement root, ReadOnlyMemory<byte> binary, JsonElement primitive)
    {
        if (!primitive.TryGetProperty("material", out var materialElement)
            || !root.TryGetProperty("materials", out var materials)
            || !root.TryGetProperty("textures", out var textures)
            || !root.TryGetProperty("images", out var images))
        {
            return null;
        }

        var material = materials[materialElement.GetInt32()];
        if (!material.TryGetProperty("pbrMetallicRoughness", out var pbr)
            || !pbr.TryGetProperty("baseColorTexture", out var baseColorTexture)
            || !baseColorTexture.TryGetProperty("index", out var textureIndexElement))
        {
            return null;
        }

        var texture = textures[textureIndexElement.GetInt32()];
        if (!texture.TryGetProperty("source", out var sourceElement))
        {
            return null;
        }

        var image = images[sourceElement.GetInt32()];
        if (!image.TryGetProperty("bufferView", out var bufferViewElement))
        {
            return null;
        }

        var bufferView = root.GetProperty("bufferViews")[bufferViewElement.GetInt32()];
        var bufferViewOffset = bufferView.TryGetProperty("byteOffset", out var viewOffsetElement) ? viewOffsetElement.GetInt32() : 0;
        var byteLength = bufferView.GetProperty("byteLength").GetInt32();
        var mimeType = image.TryGetProperty("mimeType", out var mimeElement)
            ? mimeElement.GetString() ?? "application/octet-stream"
            : "application/octet-stream";
        var name = image.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString() ?? "baseColorTexture"
            : "baseColorTexture";
        return new GlbTextureImage(name, mimeType, binary.Slice(bufferViewOffset, byteLength).ToArray());
    }

    private static (int Start, int Stride) GetAccessorLayout(JsonElement root, JsonElement accessor)
    {
        var accessorOffset = accessor.TryGetProperty("byteOffset", out var accessorOffsetElement) ? accessorOffsetElement.GetInt32() : 0;
        var bufferView = root.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
        var bufferViewOffset = bufferView.TryGetProperty("byteOffset", out var viewOffsetElement) ? viewOffsetElement.GetInt32() : 0;
        var componentType = accessor.GetProperty("componentType").GetInt32();
        var type = accessor.GetProperty("type").GetString();
        var defaultStride = ComponentSize(componentType) * ComponentCount(type);
        var stride = bufferView.TryGetProperty("byteStride", out var strideElement) ? strideElement.GetInt32() : defaultStride;
        return (bufferViewOffset + accessorOffset, stride);
    }

    private static int ComponentSize(int componentType) => componentType switch
    {
        5120 or 5121 => 1,
        5122 or 5123 => 2,
        5125 or 5126 => 4,
        _ => throw new InvalidDataException($"Unsupported GLB component type: {componentType}")
    };

    private static int ComponentCount(string? type) => type switch
    {
        "SCALAR" => 1,
        "VEC2" => 2,
        "VEC3" => 3,
        "VEC4" => 4,
        _ => throw new InvalidDataException($"Unsupported GLB accessor type: {type}")
    };

    private static uint ReadUInt32(byte[] bytes, int offset) => BitConverter.ToUInt32(bytes, offset);

    private static float ReadSingle(ReadOnlySpan<byte> bytes, int offset) => BitConverter.ToSingle(bytes.Slice(offset, 4));

    private static int Pad4(int value) => (value + 3) & ~3;
}

public static class GlbMesh
{
    public static ImportedMesh Load(string path) => ImportedMesh.Load(path);

    public static ImportedMesh CreateTriangleMesh(string path, string name, string format, Vector3[] positions, int[] indices) =>
        ImportedMesh.CreateTriangleMesh(path, name, format, positions, indices);
}

public sealed record GlbTextureImage(string Name, string MimeType, byte[] Bytes);
