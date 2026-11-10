// Spike: cuanto cuesta recibir 5000 filas del catalogo segun el formato del body.
// Mide tamano (crudo, gzip, brotli), tiempo de lectura en el servidor (mediana) y memoria asignada.
// Correr en Release: dotnet run -c Release -- [filas]
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper;
using CsvHelper.Configuration;
using MessagePack;

int rows = args.Length > 0 ? int.Parse(args[0]) : 5000;
var data = Data.Generate(rows);

var payloads = new (string Name, byte[] Body, Func<byte[], int> Read)[]
{
    ("CSV (multipart, CsvHelper)", Formats.ToCsv(data), Formats.ReadCsv),
    ("JSON objeto por fila", Formats.ToJsonObjects(data), Formats.ReadJsonObjects),
    ("JSON compacto (columnas + arreglos)", Formats.ToJsonCompact(data), Formats.ReadJsonCompact),
    ("NDJSON (una linea por fila)", Formats.ToNdjson(data), Formats.ReadNdjson),
    ("MessagePack (arreglos)", Formats.ToMsgPack(data), Formats.ReadMsgPack),
};

Console.WriteLine($"Filas: {rows}  |  .NET {Environment.Version}  |  {RuntimeInfo()}");
Console.WriteLine();
Console.WriteLine($"{"Formato",-38}{"Crudo",10}{"gzip",10}{"brotli",10}{"Lectura p50",14}{"p95",10}{"Asignado",12}");
foreach (var (name, body, read) in payloads)
{
    if (read(body) != rows) throw new InvalidOperationException($"{name}: no leyo {rows} filas");
    for (int i = 0; i < 30; i++) read(body); // calentamiento (JIT por niveles)

    var times = new List<double>();
    long alloc = 0;
    for (int i = 0; i < 200; i++)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        read(body);
        sw.Stop();
        alloc = GC.GetAllocatedBytesForCurrentThread() - before;
        times.Add(sw.Elapsed.TotalMilliseconds);
    }
    times.Sort();
    Console.WriteLine($"{name,-38}{Kb(body.Length),10}{Kb(Gzip(body)),10}{Kb(Brotli(body)),10}" +
                      $"{times[times.Count / 2],11:F2} ms{times[(int)(times.Count * 0.95)],7:F2} ms{Kb(alloc),12}");
}

static string Kb(long b) => $"{b / 1024.0:F0} KB";
static int Gzip(byte[] b) { using var ms = new MemoryStream(); using (var z = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(b); return (int)ms.Length; }
static int Brotli(byte[] b) { using var ms = new MemoryStream(); using (var z = new BrotliStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(b); return (int)ms.Length; }
static string RuntimeInfo() => $"{Environment.ProcessorCount} nucleos, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}";

// ---------------------------------------------------------------- modelo y datos

[MessagePackObject]
public sealed class ServiceRow
{
    [Key(0)] public string Code { get; set; } = "";
    [Key(1)] public string Description { get; set; } = "";
    [Key(2)] public string MeasurementUnit { get; set; } = "";
    [Key(3)] public decimal UnitPrice { get; set; }
    [Key(4)] public bool PriceIncludesTax { get; set; }
    [Key(5)] public string TaxCategory { get; set; } = "";
    [Key(6)] public string WorkType { get; set; } = "";
    [Key(7)] public string Availability { get; set; } = "";
    [Key(8)] public int? EstimatedMinutes { get; set; }
    [Key(9)] public List<string> EquipmentTypeCodes { get; set; } = [];
}

public sealed class CompactBody
{
    public List<string> Columns { get; set; } = [];
    public List<JsonElement[]> Rows { get; set; } = [];
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(List<ServiceRow>))]
[JsonSerializable(typeof(ServiceRow))]
[JsonSerializable(typeof(CompactBody))]
public partial class SpikeJson : JsonSerializerContext;

public static class Data
{
    static readonly string[] Verbs = ["Mantenimiento preventivo", "Limpieza profunda", "Instalación", "Reparación", "Diagnóstico", "Recarga de gas", "Cambio de compresor", "Revisión eléctrica"];
    static readonly string[] Things = ["minisplit", "equipo de ventana", "condensadora", "evaporadora", "paquete", "chiller", "fan & coil"];
    static readonly string[] Extra = ["", "", " hasta 2 toneladas", " incluye limpieza de serpentín", " en azotea con andamio", " con refrigerante R-410A", " (garantía 90 días)"];
    static readonly string[] Units = ["Service", "Hour", "Meter"];
    static readonly string[] Taxes = ["General", "General", "General", "ZeroRate", "Exempt"];
    static readonly string[] Work = ["Labor", "Installation", "Material"];
    static readonly string[] Avail = ["Both", "Both", "QuoteOnly", "OrderOnly"];
    static readonly string[] Equip = ["MINI", "VENT", "COND", "EVAP", "PAQ", "CHIL"];

    public static List<ServiceRow> Generate(int n)
    {
        var r = new Random(42);
        var list = new List<ServiceRow>(n);
        for (int i = 0; i < n; i++)
            list.Add(new ServiceRow
            {
                Code = $"SRV-{i + 1:D5}",
                Description = $"{Verbs[r.Next(Verbs.Length)]} de {Things[r.Next(Things.Length)]}{Extra[r.Next(Extra.Length)]}",
                MeasurementUnit = Units[r.Next(Units.Length)],
                UnitPrice = Math.Round((decimal)(r.NextDouble() * 9000 + 150), 2),
                PriceIncludesTax = r.Next(2) == 0,
                TaxCategory = Taxes[r.Next(Taxes.Length)],
                WorkType = Work[r.Next(Work.Length)],
                Availability = Avail[r.Next(Avail.Length)],
                EstimatedMinutes = r.Next(5) == 0 ? null : r.Next(15, 480),
                EquipmentTypeCodes = Enumerable.Range(0, r.Next(0, 3)).Select(_ => Equip[r.Next(Equip.Length)]).Distinct().ToList(),
            });
        return list;
    }
}

// ---------------------------------------------------------------- formatos

public static class Formats
{
    static readonly string[] Header = ["code", "description", "measurementUnit", "unitPrice", "priceIncludesTax", "taxCategory", "workType", "availability", "estimatedMinutes", "equipmentTypeCodes"];
    static readonly CsvConfiguration CsvCfg = new(CultureInfo.InvariantCulture) { PrepareHeaderForMatch = a => a.Header.ToLowerInvariant() };

    public static byte[] ToCsv(List<ServiceRow> data)
    {
        var sb = new StringBuilder(string.Join(',', Header)).Append("\r\n");
        foreach (var x in data)
            sb.Append(x.Code).Append(',').Append('"').Append(x.Description.Replace("\"", "\"\"")).Append('"').Append(',')
              .Append(x.MeasurementUnit).Append(',').Append(x.UnitPrice.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(x.PriceIncludesTax ? "true" : "false").Append(',').Append(x.TaxCategory).Append(',').Append(x.WorkType).Append(',')
              .Append(x.Availability).Append(',').Append(x.EstimatedMinutes?.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(string.Join('|', x.EquipmentTypeCodes)).Append("\r\n");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public static int ReadCsv(byte[] body)
    {
        using var reader = new StreamReader(new MemoryStream(body), new UTF8Encoding(false, throwOnInvalidBytes: true));
        using var csv = new CsvReader(reader, CsvCfg);
        csv.Read(); csv.ReadHeader();
        int n = 0;
        while (csv.Read())
        {
            var row = new ServiceRow
            {
                Code = csv.GetField(0)!,
                Description = csv.GetField(1)!,
                MeasurementUnit = csv.GetField(2)!,
                UnitPrice = decimal.Parse(csv.GetField(3)!, NumberStyles.Number, CultureInfo.InvariantCulture),
                PriceIncludesTax = bool.Parse(csv.GetField(4)!),
                TaxCategory = csv.GetField(5)!,
                WorkType = csv.GetField(6)!,
                Availability = csv.GetField(7)!,
                EstimatedMinutes = csv.GetField(8) is { Length: > 0 } m ? int.Parse(m, CultureInfo.InvariantCulture) : null,
                EquipmentTypeCodes = csv.GetField(9) is { Length: > 0 } e ? [.. e.Split('|')] : [],
            };
            n++;
        }
        return n;
    }

    public static byte[] ToJsonObjects(List<ServiceRow> data) => JsonSerializer.SerializeToUtf8Bytes(data, SpikeJson.Default.ListServiceRow);
    public static int ReadJsonObjects(byte[] body) => JsonSerializer.Deserialize(body, SpikeJson.Default.ListServiceRow)!.Count;

    public static byte[] ToJsonCompact(List<ServiceRow> data)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteStartArray("columns"); foreach (var h in Header) w.WriteStringValue(h); w.WriteEndArray();
            w.WriteStartArray("rows");
            foreach (var x in data)
            {
                w.WriteStartArray();
                w.WriteStringValue(x.Code); w.WriteStringValue(x.Description); w.WriteStringValue(x.MeasurementUnit);
                w.WriteNumberValue(x.UnitPrice); w.WriteBooleanValue(x.PriceIncludesTax); w.WriteStringValue(x.TaxCategory);
                w.WriteStringValue(x.WorkType); w.WriteStringValue(x.Availability);
                if (x.EstimatedMinutes is { } m) w.WriteNumberValue(m); else w.WriteNullValue();
                w.WriteStartArray(); foreach (var e in x.EquipmentTypeCodes) w.WriteStringValue(e); w.WriteEndArray();
                w.WriteEndArray();
            }
            w.WriteEndArray(); w.WriteEndObject();
        }
        return ms.ToArray();
    }

    // El formato compacto obliga a validar a mano el orden de columnas y cada posicion.
    public static int ReadJsonCompact(byte[] body)
    {
        var b = JsonSerializer.Deserialize(body, SpikeJson.Default.CompactBody)!;
        if (!b.Columns.SequenceEqual(Header)) throw new InvalidOperationException("columnas");
        int n = 0;
        foreach (var a in b.Rows)
        {
            if (a.Length != Header.Length) throw new InvalidOperationException("ancho");
            var row = new ServiceRow
            {
                Code = a[0].GetString()!, Description = a[1].GetString()!, MeasurementUnit = a[2].GetString()!,
                UnitPrice = a[3].GetDecimal(), PriceIncludesTax = a[4].GetBoolean(), TaxCategory = a[5].GetString()!,
                WorkType = a[6].GetString()!, Availability = a[7].GetString()!,
                EstimatedMinutes = a[8].ValueKind == JsonValueKind.Null ? null : a[8].GetInt32(),
                EquipmentTypeCodes = [.. a[9].EnumerateArray().Select(e => e.GetString()!)],
            };
            n++;
        }
        return n;
    }

    public static byte[] ToNdjson(List<ServiceRow> data)
    {
        using var ms = new MemoryStream();
        foreach (var x in data) { ms.Write(JsonSerializer.SerializeToUtf8Bytes(x, SpikeJson.Default.ServiceRow)); ms.WriteByte((byte)'\n'); }
        return ms.ToArray();
    }

    public static int ReadNdjson(byte[] body)
    {
        int n = 0, start = 0;
        var span = body.AsSpan();
        while (start < span.Length)
        {
            int nl = span[start..].IndexOf((byte)'\n');
            var line = nl < 0 ? span[start..] : span.Slice(start, nl);
            JsonSerializer.Deserialize(line, SpikeJson.Default.ServiceRow);
            n++;
            start += (nl < 0 ? line.Length : nl) + 1;
        }
        return n;
    }

    public static byte[] ToMsgPack(List<ServiceRow> data) => MessagePackSerializer.Serialize(data);
    public static int ReadMsgPack(byte[] body) => MessagePackSerializer.Deserialize<List<ServiceRow>>(body).Count;
}
