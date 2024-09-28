using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace XML_parsing;

internal static class Program
{
    private static DateOnly _date;
    private static XDocument _fullXml = new XDocument();
    
    private static readonly HttpClient HttpClient = new();
    private static async Task Main(string[] args)
    {
        await GetFile();
        ReadZip();
        File.Delete("data.zip");
        File.Delete("version.txt");
        _fullXml.Save("full.xml");
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
        foreach (var entry in zipFile.Entries)
        {
            if (entry.Name == "version.txt")
            {
                using var stream = entry.Open();
                var reader = new StreamReader(stream);
                ParseDate(reader.ReadLine());
            }

            if (entry.Name.StartsWith($"AS_ADDR_OBJ_2"))
            {
                using var stream = entry.Open();
                AddXml(stream);
            }
        }
    }

    private static void ParseDate(string? date)
    {
        var dateParts = date!.Split('.');
        _date = new DateOnly(int.Parse(dateParts[0]), int.Parse(dateParts[1]), int.Parse(dateParts[2]));
    }

    private static void AddXml(Stream xmlStream)
    {
        var xDoc = XDocument.Load(xmlStream);
        if (!_fullXml.Descendants().Any())
            _fullXml = xDoc;
        else
            _fullXml.Root?.Add(xDoc.Descendants("OBJECT"));
    }
}