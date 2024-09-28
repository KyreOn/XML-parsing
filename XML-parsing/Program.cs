using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace XML_parsing;

internal static class Program
{
    private static DateOnly _date;
    private static XDocument _objectXml = new XDocument();
    private static XDocument _levelXml = new XDocument();
    private static string[] _objectLevels = Array.Empty<string>();
    private static IEnumerable<IGrouping<string, string[]>>? _finalResults;
    
    private static readonly HttpClient HttpClient = new();
    private static async Task Main(string[] args)
    {
        await GetFile();
        ReadZip();
        File.Delete("data.zip");
        File.Delete("version.txt");
        _objectXml.Save("full.xml");
        _finalResults = Query();
        foreach (var group in _finalResults)
        {
            foreach (var item in group)
            {
                Console.WriteLine(string.Join(" ", item));
            }
        }
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

            if (entry.Name.StartsWith("AS_OBJECT_LEVELS"))
            {
                using var stream = entry.Open();
                GetObjectLevels(stream);
            }
            //TODO сделать условие более универсальным
            if (entry.Name.StartsWith("AS_ADDR_OBJ_2"))
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

    private static void GetObjectLevels(Stream xmlStream)
    {
        var levelXml =  XDocument.Load(xmlStream);
        _levelXml = levelXml;
        var result = levelXml.Descendants("OBJECTLEVEL").Select(x => x.Attribute("NAME").Value).ToArray();
        _objectLevels = result;
    }

    private static void AddXml(Stream xmlStream)
    {
        var xDoc = XDocument.Load(xmlStream);
        if (!_objectXml.Descendants().Any())
            _objectXml = xDoc;
        else
            _objectXml.Root?.Add(xDoc.Descendants("OBJECT"));
    }

    private static IEnumerable<IGrouping<string, string[]>> Query()
    {
        var groupedResult = _objectXml.Descendants("OBJECT").Join(_levelXml.Descendants("OBJECTLEVEL"), x => x.Attribute("LEVEL").Value, y => y.Attribute("LEVEL").Value, (x, y)
                => new
                {
                    IsActive = x.Attribute("ISACTIVE"), Name = x.Attribute("NAME"), TypeName = x.Attribute("TYPENAME"),
                    Level = x.Attribute("LEVEL"), LevelName = y.Attribute("NAME")
                })
            .Where(x => x.IsActive.Value == "1")
            .Select(x => new [] {x.Name.Value, x.TypeName.Value, x.Level.Value, x.LevelName.Value})
            .OrderBy(x => x[0]).GroupBy(x => x[2]);
        return groupedResult;
    }

    private static void CreateDocument()
    {
        
    }
}