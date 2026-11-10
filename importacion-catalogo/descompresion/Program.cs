// Spike: Content-Encoding: gzip en la PETICION con el middleware nativo de ASP.NET Core.
// Pregunta: el limite del body se cuenta sobre lo descomprimido? (defensa contra bomba de descompresion)
// Levanta Kestrel en un puerto libre, se manda peticiones a si mismo e imprime el resultado.
using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;

const long Limit = 4 * 1024 * 1024; // 4 MB, el tope propuesto para el body de la importacion

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddRequestDecompression(); // gzip, deflate y br por defecto
var app = builder.Build();
app.UseRequestDecompression();

app.MapPost("/import", [RequestSizeLimit(Limit)] async (HttpRequest req) =>
{
    using var ms = new MemoryStream();
    await req.Body.CopyToAsync(ms);
    return Results.Ok(new { bytesLeidos = ms.Length });
});

await app.StartAsync();
var url = app.Urls.First() + "/import";
using var http = new HttpClient();

async Task Send(string caso, byte[] raw, bool gzip)
{
    byte[] body = gzip ? Gzip(raw) : raw;
    using var content = new ByteArrayContent(body);
    content.Headers.ContentType = new("application/json");
    if (gzip) content.Headers.ContentEncoding.Add("gzip");
    try
    {
        var res = await http.PostAsync(url, content);
        Console.WriteLine($"{caso,-46} enviado {Kb(body.Length),9}  descomprimido {Kb(raw.Length),10}  -> {(int)res.StatusCode} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
    }
    catch (Exception ex) { Console.WriteLine($"{caso,-46} -> excepcion {ex.GetType().Name}: {ex.Message}"); }
}

// 1.4 MB de JSON parecido a la importacion real (5000 filas)
var sb = new StringBuilder("[");
for (int i = 0; i < 5000; i++) sb.Append($"{{\"code\":\"SRV-{i:D5}\",\"name\":\"Mantenimiento preventivo de minisplit {i % 7}\",\"unitPrice\":\"{1000 + i}.50\",\"taxCategory\":\"General\"}},");
sb[^1] = ']';
var json = Encoding.UTF8.GetBytes(sb.ToString());

await Send("JSON normal, sin comprimir", json, gzip: false);
await Send("JSON normal, gzip", json, gzip: true);
await Send("Bomba: 200 MB de ceros en gzip", new byte[200 * 1024 * 1024], gzip: true);
await Send("Bomba justo arriba del limite (4 MB + 1)", new byte[Limit + 1], gzip: true);
await Send("Sin comprimir, arriba del limite", new byte[Limit + 1], gzip: false);

await app.StopAsync();

static byte[] Gzip(byte[] b) { using var ms = new MemoryStream(); using (var z = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(b); return ms.ToArray(); }
static string Kb(long b) => b >= 1024 * 1024 ? $"{b / 1048576.0:F1} MB" : $"{b / 1024.0:F0} KB";
