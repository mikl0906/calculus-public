using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalculusModel;

public static class CalculusModelSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IncludeFields = true,
        WriteIndented = true,
        Converters = {
            new WeldPositionConverter(),
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new ModelObjectJsonConverter(),
            new CutOperativePartJsonConverter(),
            new ChamferJsonConverter(),
        },
    };
    public static string Serialize(Model model)
    {
        return JsonSerializer.Serialize(model, Options);
    }
    public static Model? Deserialize(string json)
    {
        var model = JsonSerializer.Deserialize<Model>(json, Options);
        model?.Objects.RemoveAll(o => o is null);
        return model;
    }

    /// <summary>
    /// Serialize a bare list of objects to a JSON array, using the same
    /// polymorphic converters as <see cref="Serialize"/>. Used to build the
    /// <c>{ "objects": [...] }</c> payload the Calculus bridge API expects.
    /// </summary>
    public static string SerializeObjects(IEnumerable<ModelObject> objects)
    {
        return JsonSerializer.Serialize(objects, Options);
    }

    /// <summary>
    /// Deserialize a JSON array of objects (as returned by the bridge's
    /// <c>getAllObjects</c>/<c>getSelection</c>). Unknown object types
    /// deserialize to null and are dropped.
    /// </summary>
    public static List<ModelObject> DeserializeObjects(string json)
    {
        var objects = JsonSerializer.Deserialize<List<ModelObject>>(json, Options) ?? [];
        objects.RemoveAll(o => o is null);
        return objects;
    }
}

public class ModelObjectJsonConverter : JsonConverter<ModelObject>
{
    public override ModelObject? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.TryGetProperty("type", out var typeProp))
        {
            var typeStr = typeProp.GetString();
            switch (typeStr)
            {
                case "beam":
                    return JsonSerializer.Deserialize<Beam>(root.GetRawText(), options);
                case "plate":
                    return JsonSerializer.Deserialize<Plate>(root.GetRawText(), options);
                case "cutPart":
                    return JsonSerializer.Deserialize<CutPart>(root.GetRawText(), options);
                case "cutPlane":
                    return JsonSerializer.Deserialize<CutPlane>(root.GetRawText(), options);
                case "bolt":
                    return JsonSerializer.Deserialize<Bolt>(root.GetRawText(), options);
                case "weld":
                    return JsonSerializer.Deserialize<Weld>(root.GetRawText(), options);
                case "rebar":
                    return JsonSerializer.Deserialize<Rebar>(root.GetRawText(), options);
                case "grid":
                    return JsonSerializer.Deserialize<Grid>(root.GetRawText(), options);
                case "line":
                    return JsonSerializer.Deserialize<Line>(root.GetRawText(), options);
            }
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, ModelObject value, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value, value.GetType(), options));
        writer.WriteStartObject();
        // Write type property
        string typeStr = value switch
        {
            Beam => "beam",
            Plate => "plate",
            CutPart => "cutPart",
            CutPlane => "cutPlane",
            Bolt => "bolt",
            Weld => "weld",
            Rebar => "rebar",
            Grid => "grid",
            Line => "line",
            _ => "unknown"
        };
        writer.WriteString("type", typeStr);
        // Write all other properties
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.NameEquals("type")) continue; // Avoid duplicate
            prop.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}

public class CutOperativePartJsonConverter : JsonConverter<Cutter>
{
    public override Cutter? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.TryGetProperty("type", out var typeProp))
        {
            var typeStr = typeProp.GetString();
            switch (typeStr?.ToLowerInvariant())
            {
                case "beam":
                    return JsonSerializer.Deserialize<CutBeam>(root.GetRawText(), options);
                case "plate":
                    return JsonSerializer.Deserialize<CutPlate>(root.GetRawText(), options);
            }
        }
        throw new JsonException($"Unknown CutOperativePart type: {root}");
    }

    public override void Write(Utf8JsonWriter writer, Cutter value, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value, value.GetType(), options));
        writer.WriteStartObject();
        // Write type property
        string typeStr = value switch
        {
            CutBeam => "beam",
            CutPlate => "plate",
            _ => "unknown"
        };
        writer.WriteString("type", typeStr);
        // Write all other properties
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.NameEquals("type")) continue; // Avoid duplicate
            prop.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}

public class ChamferJsonConverter : JsonConverter<Chamfer>
{
    public override Chamfer? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.TryGetProperty("type", out var typeProp))
        {
            var typeStr = typeProp.GetString();
            return typeStr switch
            {
                "line" => JsonSerializer.Deserialize<LineChamfer>(root.GetRawText(), options),
                "round" => JsonSerializer.Deserialize<RoundChamfer>(root.GetRawText(), options),
                "arc" => JsonSerializer.Deserialize<ArcChamfer>(root.GetRawText(), options),
                "arcPoint" => JsonSerializer.Deserialize<ArcPointChamfer>(root.GetRawText(), options),
                "square" => JsonSerializer.Deserialize<SquareChamfer>(root.GetRawText(), options),
                "squareParallel" => JsonSerializer.Deserialize<SquareParallelChamfer>(root.GetRawText(), options),
                "lineAndArc" => JsonSerializer.Deserialize<LineAndArcChamfer>(root.GetRawText(), options),
                _ => throw new JsonException($"Unknown Chamfer type: {typeStr}")
            };
        }
        throw new JsonException($"Unknown Chamfer type: {root}");
    }

    public override void Write(Utf8JsonWriter writer, Chamfer value, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value, value.GetType(), options));
        writer.WriteStartObject();
        // Write type property
        string typeStr = value switch
        {
            LineChamfer => "line",
            RoundChamfer => "round",
            ArcChamfer => "arc",
            ArcPointChamfer => "arcPoint",
            SquareChamfer => "square",
            SquareParallelChamfer => "squareParallel",
            LineAndArcChamfer => "lineAndArc",
            _ => "unknown"
        };
        writer.WriteString("type", typeStr);
        // Write all other properties
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.NameEquals("type")) continue; // Avoid duplicate
            prop.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}

public class WeldPositionConverter : JsonConverter<WeldPosition>
{
    public override WeldPosition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected string, got {reader.TokenType}");

        var position = reader.GetString()?.ToLowerInvariant();

        return position switch
        {
            "x+" => WeldPosition.XPlus,
            "x-" => WeldPosition.XMinus,
            "y+" => WeldPosition.YPlus,
            "y-" => WeldPosition.YMinus,
            "z+" => WeldPosition.ZPlus,
            "z-" => WeldPosition.ZMinus,
            _ => throw new JsonException($"Unknown WeldPosition value: {position}")
        };
    }

    public override void Write(Utf8JsonWriter writer, WeldPosition value, JsonSerializerOptions options)
    {
        string position = value switch
        {
            WeldPosition.XPlus => "x+",
            WeldPosition.XMinus => "x-",
            WeldPosition.YPlus => "y+",
            WeldPosition.YMinus => "y-",
            WeldPosition.ZPlus => "z+",
            WeldPosition.ZMinus => "z-",
            _ => throw new JsonException($"Unknown WeldPosition type: {value}"),
        };

        writer.WriteStringValue(position);
    }
}