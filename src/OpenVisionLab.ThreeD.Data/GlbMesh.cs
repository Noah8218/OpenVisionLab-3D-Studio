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
            var meshName = root.GetProperty("meshes")[0].TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? Path.GetFileNameWithoutExtension(path)
                : Path.GetFileNameWithoutExtension(path);
            var positions = new List<Vector3>();
            var indices = new List<int>();
            var vertexColors = new List<Vector4>();
            var textureCoordinates = new List<Vector2>();
            var keepVertexColors = true;
            var keepTextureCoordinates = true;
            GlbTextureImage? texture = null;

            if (root.TryGetProperty("scenes", out var scenes)
                && root.TryGetProperty("nodes", out _))
            {
                var sceneIndex = root.TryGetProperty("scene", out var sceneElement) ? sceneElement.GetInt32() : 0;
                var scene = scenes[sceneIndex];
                if (scene.TryGetProperty("nodes", out var sceneNodes))
                {
                    foreach (var nodeElement in sceneNodes.EnumerateArray())
                    {
                        AppendNodeMeshes(
                            root,
                            binaryChunk,
                            path,
                            nodeElement.GetInt32(),
                            Matrix4x4.Identity,
                            positions,
                            indices,
                            vertexColors,
                            ref keepVertexColors,
                            textureCoordinates,
                            ref keepTextureCoordinates,
                            ref texture);
                    }
                }
            }

            if (positions.Count == 0)
            {
                AppendMeshPrimitives(
                    root,
                    binaryChunk,
                    path,
                    0,
                    Matrix4x4.Identity,
                    positions,
                    indices,
                    vertexColors,
                    ref keepVertexColors,
                    textureCoordinates,
                    ref keepTextureCoordinates,
                    ref texture);
            }

            var meshPositions = positions.ToArray();
            var meshIndices = indices.ToArray();
            var meshVertexColors = keepVertexColors && vertexColors.Count == meshPositions.Length
                ? vertexColors.ToArray()
                : Array.Empty<Vector4>();
            var meshTextureCoordinates = keepTextureCoordinates && textureCoordinates.Count == meshPositions.Length
                ? textureCoordinates.ToArray()
                : Array.Empty<Vector2>();

            if (meshPositions.Length == 0 || meshIndices.Length == 0 || meshIndices.Length % 3 != 0)
            {
                throw new InvalidDataException($"GLB mesh has no complete triangles: {path}");
            }

            foreach (var index in meshIndices)
            {
                if (index < 0 || index >= meshPositions.Length)
                {
                    throw new InvalidDataException($"GLB mesh index is outside the vertex range: {path}");
                }
            }

            return new ImportedMesh(path, meshName, "GLB", meshPositions, meshIndices, meshVertexColors, meshTextureCoordinates, texture);
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

    private static void AppendNodeMeshes(
        JsonElement root,
        ReadOnlyMemory<byte> binary,
        string path,
        int nodeIndex,
        Matrix4x4 parentTransform,
        List<Vector3> positions,
        List<int> indices,
        List<Vector4> vertexColors,
        ref bool keepVertexColors,
        List<Vector2> textureCoordinates,
        ref bool keepTextureCoordinates,
        ref GlbTextureImage? texture)
    {
        var node = root.GetProperty("nodes")[nodeIndex];
        var nodeTransform = ReadNodeTransform(node) * parentTransform;
        if (node.TryGetProperty("mesh", out var meshElement))
        {
            foreach (var instanceTransform in ReadInstanceTransforms(root, binary.Span, node, nodeTransform))
            {
                AppendMeshPrimitives(
                    root,
                    binary,
                    path,
                    meshElement.GetInt32(),
                    instanceTransform,
                    positions,
                    indices,
                    vertexColors,
                    ref keepVertexColors,
                    textureCoordinates,
                    ref keepTextureCoordinates,
                    ref texture);
            }
        }

        if (!node.TryGetProperty("children", out var children))
        {
            return;
        }

        foreach (var childElement in children.EnumerateArray())
        {
            AppendNodeMeshes(
                root,
                binary,
                path,
                childElement.GetInt32(),
                nodeTransform,
                positions,
                indices,
                vertexColors,
                ref keepVertexColors,
                textureCoordinates,
                ref keepTextureCoordinates,
                ref texture);
        }
    }

    private static void AppendMeshPrimitives(
        JsonElement root,
        ReadOnlyMemory<byte> binary,
        string path,
        int meshIndex,
        Matrix4x4 transform,
        List<Vector3> positions,
        List<int> indices,
        List<Vector4> vertexColors,
        ref bool keepVertexColors,
        List<Vector2> textureCoordinates,
        ref bool keepTextureCoordinates,
        ref GlbTextureImage? texture)
    {
        var mesh = root.GetProperty("meshes")[meshIndex];
        foreach (var primitive in mesh.GetProperty("primitives").EnumerateArray())
        {
            var mode = primitive.TryGetProperty("mode", out var modeElement) ? modeElement.GetInt32() : 4;
            if (mode != 4)
            {
                throw new InvalidDataException($"Only GLB triangle primitives are supported: {path}");
            }

            var attributes = primitive.GetProperty("attributes");
            var primitivePositions = ReadVec3Accessor(root, binary.Span, attributes.GetProperty("POSITION").GetInt32());
            var primitiveIndices = primitive.TryGetProperty("indices", out var indexElement)
                ? ReadIndexAccessor(root, binary.Span, indexElement.GetInt32())
                : Enumerable.Range(0, primitivePositions.Length).ToArray();
            var primitiveVertexColors = attributes.TryGetProperty("COLOR_0", out var colorElement)
                ? ReadColorAccessor(root, binary.Span, colorElement.GetInt32())
                : Array.Empty<Vector4>();
            var primitiveTextureCoordinates = attributes.TryGetProperty("TEXCOORD_0", out var texCoordElement)
                ? ReadVec2Accessor(root, binary.Span, texCoordElement.GetInt32())
                : Array.Empty<Vector2>();

            if (primitivePositions.Length == 0 || primitiveIndices.Length == 0 || primitiveIndices.Length % 3 != 0)
            {
                throw new InvalidDataException($"GLB mesh has no complete triangles: {path}");
            }

            if (primitiveVertexColors.Length > 0 && primitiveVertexColors.Length != primitivePositions.Length)
            {
                throw new InvalidDataException($"GLB COLOR_0 count must match POSITION count: {path}");
            }

            if (primitiveTextureCoordinates.Length > 0 && primitiveTextureCoordinates.Length != primitivePositions.Length)
            {
                throw new InvalidDataException($"GLB TEXCOORD_0 count must match POSITION count: {path}");
            }

            var vertexOffset = positions.Count;
            foreach (var position in primitivePositions)
            {
                positions.Add(Vector3.Transform(position, transform));
            }

            foreach (var index in primitiveIndices)
            {
                indices.Add(vertexOffset + index);
            }

            AppendOptionalVertexData(primitiveVertexColors, primitivePositions.Length, vertexColors, ref keepVertexColors);
            AppendOptionalVertexData(primitiveTextureCoordinates, primitivePositions.Length, textureCoordinates, ref keepTextureCoordinates);
            texture ??= ReadBaseColorTexture(root, binary, primitive);
        }
    }

    private static void AppendOptionalVertexData<T>(
        T[] source,
        int positionCount,
        List<T> target,
        ref bool keepData)
    {
        if (!keepData)
        {
            return;
        }

        if (source.Length != positionCount)
        {
            keepData = false;
            target.Clear();
            return;
        }

        target.AddRange(source);
    }

    private static Matrix4x4 ReadNodeTransform(JsonElement node)
    {
        if (node.TryGetProperty("matrix", out var matrixElement))
        {
            var values = matrixElement.EnumerateArray().Select(item => item.GetSingle()).ToArray();
            if (values.Length != 16)
            {
                throw new InvalidDataException("GLB node matrix must contain 16 values.");
            }

            return new Matrix4x4(
                values[0], values[1], values[2], values[3],
                values[4], values[5], values[6], values[7],
                values[8], values[9], values[10], values[11],
                values[12], values[13], values[14], values[15]);
        }

        var translation = ReadVector3Property(node, "translation", Vector3.Zero);
        var rotation = ReadQuaternionProperty(node, "rotation", Quaternion.Identity);
        var scale = ReadVector3Property(node, "scale", Vector3.One);
        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
    }

    private static Matrix4x4[] ReadInstanceTransforms(JsonElement root, ReadOnlySpan<byte> binary, JsonElement node, Matrix4x4 nodeTransform)
    {
        if (!node.TryGetProperty("extensions", out var extensions)
            || !extensions.TryGetProperty("EXT_mesh_gpu_instancing", out var instancing)
            || !instancing.TryGetProperty("attributes", out var attributes))
        {
            return [nodeTransform];
        }

        var translations = attributes.TryGetProperty("TRANSLATION", out var translationElement)
            ? ReadVec3Accessor(root, binary, translationElement.GetInt32())
            : Array.Empty<Vector3>();
        var rotations = attributes.TryGetProperty("ROTATION", out var rotationElement)
            ? ReadVec4Accessor(root, binary, rotationElement.GetInt32())
            : Array.Empty<Vector4>();
        var scales = attributes.TryGetProperty("SCALE", out var scaleElement)
            ? ReadVec3Accessor(root, binary, scaleElement.GetInt32())
            : Array.Empty<Vector3>();

        var instanceCount = new[] { translations.Length, rotations.Length, scales.Length }.Max();
        if (instanceCount == 0)
        {
            return [nodeTransform];
        }

        ValidateOptionalInstanceAccessor("TRANSLATION", translations.Length, instanceCount);
        ValidateOptionalInstanceAccessor("ROTATION", rotations.Length, instanceCount);
        ValidateOptionalInstanceAccessor("SCALE", scales.Length, instanceCount);

        var transforms = new Matrix4x4[instanceCount];
        for (var i = 0; i < transforms.Length; i++)
        {
            var translation = translations.Length == 0 ? Vector3.Zero : translations[i];
            var rotation = rotations.Length == 0
                ? Quaternion.Identity
                : Quaternion.Normalize(new Quaternion(rotations[i].X, rotations[i].Y, rotations[i].Z, rotations[i].W));
            var scale = scales.Length == 0 ? Vector3.One : scales[i];
            transforms[i] = Matrix4x4.CreateScale(scale)
                * Matrix4x4.CreateFromQuaternion(rotation)
                * Matrix4x4.CreateTranslation(translation)
                * nodeTransform;
        }

        return transforms;
    }

    private static void ValidateOptionalInstanceAccessor(string name, int count, int instanceCount)
    {
        if (count is not 0 && count != instanceCount)
        {
            throw new InvalidDataException($"GLB EXT_mesh_gpu_instancing {name} count must match instance count.");
        }
    }

    private static Vector3 ReadVector3Property(JsonElement element, string propertyName, Vector3 fallback)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return fallback;
        }

        var values = property.EnumerateArray().Select(item => item.GetSingle()).ToArray();
        if (values.Length != 3)
        {
            throw new InvalidDataException($"GLB {propertyName} must contain 3 values.");
        }

        return new Vector3(values[0], values[1], values[2]);
    }

    private static Quaternion ReadQuaternionProperty(JsonElement element, string propertyName, Quaternion fallback)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return fallback;
        }

        var values = property.EnumerateArray().Select(item => item.GetSingle()).ToArray();
        if (values.Length != 4)
        {
            throw new InvalidDataException($"GLB {propertyName} must contain 4 values.");
        }

        return Quaternion.Normalize(new Quaternion(values[0], values[1], values[2], values[3]));
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

    private static Vector4[] ReadVec4Accessor(JsonElement root, ReadOnlySpan<byte> binary, int accessorIndex)
    {
        var accessor = root.GetProperty("accessors")[accessorIndex];
        if (accessor.GetProperty("componentType").GetInt32() != 5126
            || accessor.GetProperty("type").GetString() != "VEC4")
        {
            throw new InvalidDataException("Only float VEC4 accessors are supported for GLB instance rotations.");
        }

        var count = accessor.GetProperty("count").GetInt32();
        var (start, stride) = GetAccessorLayout(root, accessor);
        var values = new Vector4[count];
        for (var i = 0; i < count; i++)
        {
            var itemOffset = start + i * stride;
            values[i] = new Vector4(
                ReadSingle(binary, itemOffset),
                ReadSingle(binary, itemOffset + 4),
                ReadSingle(binary, itemOffset + 8),
                ReadSingle(binary, itemOffset + 12));
        }

        return values;
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
