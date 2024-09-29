using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace XML_parsing;

internal static class Program
{
    private static string _date = "";
    private static XDocument _objectXml = new();
    private static XDocument _levelXml = new();
    private static IEnumerable<IGrouping<string, string[]>> _finalResults = new List<IGrouping<string, string[]>>();
    private static readonly HttpClient HttpClient = new();
    
    private static async Task Main()
    {
        await GetFile();
        ReadZip();
        Query();
        CreateDocument();
        var p = new Process();
        p.StartInfo = new ProcessStartInfo("report.html")
        { 
            UseShellExecute = true 
        };
        p.Start();
        CleanUp();
    }   
    
    //Получение ZIP-архива
    private static async Task GetFile()
    {
        var response = await HttpClient.GetAsync("http://fias.nalog.ru/WebServices/Public/GetLastDownloadFileInfo");
        var jsonResponse = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        using var s = HttpClient.GetStreamAsync(jsonResponse["GarXMLDeltaURL"].ToString());
        await using var fs = new FileStream("data.zip", FileMode.OpenOrCreate);
        await s.Result.CopyToAsync(fs);
    }
  
    //Обработка содержимого ZIP-архива
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
            if (entry.Name.StartsWith("AS_ADDR_OBJ_2"))
            {
                using var stream = entry.Open();
                AddXml(stream);
            }
        }
    }

    //Приведение даты к необходимому формату
    private static void ParseDate(string date)
    {
        var dateParts = date.Split('.');
        _date = $"{dateParts[2]}.{dateParts[1]}.{dateParts[0]}";
    }
    
    //Получение списка уровней адресных объектов
    private static void GetObjectLevels(Stream xmlStream)
    {
        var levelXml =  XDocument.Load(xmlStream);
        _levelXml = levelXml;
    }
    
    //Объединение XML-документов
    private static void AddXml(Stream xmlStream)
    {
        var xDoc = XDocument.Load(xmlStream);
        if (!_objectXml.Descendants().Any())
            _objectXml = xDoc;
        else
            _objectXml.Root.Add(xDoc.Descendants("OBJECT"));
    }
    
    //Отбор полученных данных
    private static void Query()
    {
        var groupedResult = _objectXml.Descendants("OBJECT")
            .Join(_levelXml.Descendants("OBJECTLEVEL"), x => x.Attribute("LEVEL").Value,
                y => y.Attribute("LEVEL").Value, (x, y) => new
                {
                    IsActive = x.Attribute("ISACTIVE"), Name = x.Attribute("NAME"), TypeName = x.Attribute("TYPENAME"),
                    Level = x.Attribute("LEVEL"), LevelName = y.Attribute("NAME")
                })
            .Where(x => x.IsActive.Value == "1")
            .Select(x => new [] {x.Name.Value, x.TypeName.Value, x.Level.Value, x.LevelName.Value})
            .OrderBy(x => x[0]).ThenBy(x => x[2]).GroupBy(x => x[3]);
        _finalResults = groupedResult;
    }
    
    //Формирование HTML-документа на основе отобранных данных
    private static void CreateDocument()
    {
        var html = new StringBuilder();
        
        html.AppendLine("<head>");
        html.AppendLine("<link rel=\"stylesheet\" href=\"report.css\">");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine($"<h3>Отчет по добавленным адресным объектам за {_date}</h3>");
        html.AppendLine("<hr>");
        foreach (var group in _finalResults!)
        {
            html.AppendLine("<Table>");
            html.AppendLine($"<Caption>{group.Key}</Caption>");
            html.AppendLine("<TR>");
            html.AppendLine("<TH>Тип объекта</TH>");
            html.AppendLine("<TH>Наименование</TH>");
            html.AppendLine("</TR>");
            foreach (var element in group)
            {
                html.AppendLine("<TR>");
                html.AppendLine($"<TD>{element[1]}</TD>");
                html.AppendLine($"<TD>{element[0]}</TD>");
                html.AppendLine("</TR>");
            }
            html.AppendLine("</Table>");
        }
        html.AppendLine("</body>");
        File.WriteAllText("report.html", html.ToString());
    }
    
    //Удаление ненужных файлов
    private static void CleanUp()
    {
        File.Delete("data.zip");
    }
}