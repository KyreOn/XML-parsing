using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace XML_parsing;

internal static class Program
{
    private static readonly HttpClient HttpClient = new();
    private static async Task Main(string[] args)
    {
        await GetFile();
        ReadZip();
        File.Delete("data.zip");
    }

    private static async Task GetFile()
    {
        var response = await HttpClient.GetAsync("http://fias.nalog.ru/WebServices/Public/GetLastDownloadFileInfo");
        var jsonResponse = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        using var s = HttpClient.GetStreamAsync(jsonResponse?["GarXMLDeltaURL"]?.ToString());
        await using var fs = new FileStream("data.zip", FileMode.OpenOrCreate);
        await s.Result.CopyToAsync(fs);
    }

    private static void ReadZip()
    {
        using var zipFile = ZipFile.OpenRead("data.zip");
        var counter = 0;
        foreach (var entry in zipFile.Entries)
            Console.WriteLine($"{++counter,3}: {entry.Name}");
    }
}