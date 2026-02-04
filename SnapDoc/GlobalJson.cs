#nullable disable

using SnapDoc.Models;
using System.Text.Json;
using SnapDoc.Services;

namespace SnapDoc;

public static class GlobalJson
{
    private static JsonDataModel _userData = new();
    private static string _filePath;

    public static JsonDataModel Data
    {
        get => _userData;
        set => _userData = value;
    }

    public static void UpdateFilePath(string filePath)
    {
        _filePath = filePath;
    }

    public static JsonSerializerOptions GetOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new SKColorConverter() },
            //DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault  //Ignoriert "IsEmpty": false und "IsZero": false,
        };
    }

    public static string ToJson()
    {
        var options = GetOptions();
        return JsonSerializer.Serialize(_userData, options);
    }

    public static void FromJson(string json)
    {
        var options = GetOptions();
        _userData = JsonSerializer.Deserialize<JsonDataModel>(json, options);
    }

    public static void SaveToFile()
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            Console.WriteLine("Kein Dateipfad festgelegt. Laden Sie zuerst eine Datei.");
            return;
        }

        try
        {
            string json = ToJson(); // Serialisiere mit den Optionen
            json = json.Replace("\r\n", "\n").Replace("\r", "\n"); // Zeilenumbrüche für Android anpassen

            File.WriteAllText(_filePath, json); // Überschreibe die Datei mit den neuen Daten
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Speichern der Datei: {ex.Message}");
        }
    }

    public static void LoadFromFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                _filePath = filePath; // Speichere den Dateipfad
                string json = File.ReadAllText(filePath);
                FromJson(json); // Deserialisiere mit den Optionen
            }
            else
            {
                Console.WriteLine($"Datei existiert nicht: {filePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Laden der Datei: {ex.Message}");
        }
    }

    public static void CreateNewFile(string filePath)
    {
        try
        {
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                Directory.CreateDirectory(Path.Combine(directoryPath, "images"));
                Directory.CreateDirectory(Path.Combine(directoryPath, "images", "originals"));
                Directory.CreateDirectory(Path.Combine(directoryPath, "plans"));
                Directory.CreateDirectory(Path.Combine(directoryPath, "plans", "thumbnails"));
                Directory.CreateDirectory(Path.Combine(directoryPath, "thumbnails"));
            }

            File.WriteAllText(filePath, ""); // Erstellt eine neue Datei;
            _filePath = filePath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Erstellen der Datei: {ex.Message}");
        }
    }

    public static String GetFilePath()
    {
        return _filePath;
    }
}