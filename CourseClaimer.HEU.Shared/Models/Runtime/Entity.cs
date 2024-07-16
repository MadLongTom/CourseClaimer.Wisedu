// See https://aka.ms/new-console-template for more information
#pragma warning disable CS8618
using System.Diagnostics;

public record Entity(string username, string password, List<string> category, List<string> classname, List<Row> done, bool finished, string? batchId)
{
    public HttpClient client { get; set; }
    public Stopwatch stopwatch = Stopwatch.StartNew();
    public bool finished { get; set; } = finished;
    public string? batchId { get; set; } = batchId;
};