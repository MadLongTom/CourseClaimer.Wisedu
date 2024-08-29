// See https://aka.ms/new-console-template for more information
#pragma warning disable CS8618
using System.Diagnostics;
using CourseClaimer.Wisedu.Shared.Dto;
using CourseClaimer.Wisedu.Shared.Models.JWXK;

namespace CourseClaimer.Wisedu.Shared.Models.Runtime;

public record Entity(string username, string password, List<string> category, List<string> courses, List<Row> done, bool finished, string? batchId,int priority)
{
    public bool IsAddPending { get; set; } = false;
    public HttpClient client { get; set; }
    public List<string> SubscribedRows { get; set; } = [];
    public List<RowSecretDto> Secrets { get; set; } = [];
    public Stopwatch stopwatch = Stopwatch.StartNew();
    public bool finished { get; set; } = finished;
    public string? batchId { get; set; } = batchId;
    public int priority { get; set; } = priority;
};