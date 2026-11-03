// C# adaptation of ReClaim's rules.rs and paths.rs.
// Copyright (c) 2026 yusufky63 and contributors. MIT; see ThirdParty/Reclaim/LICENSE.txt.
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FromZero;

public sealed record ReclaimRule
{
 public string Id { get; init; } = "";
 public string Group { get; init; } = "";
 public string NameTr { get; init; } = "";
 public string DescriptionTr { get; init; } = "";
 public string[] Paths { get; init; } = [];
 public string Mode { get; init; } = "";
 public string[] Include { get; init; } = [];
 public string[] Exclude { get; init; } = [];
 public int? MinAgeHours { get; init; }
 public string Risk { get; init; } = "";
 public bool Admin { get; init; }
 public string[] CloseApps { get; init; } = [];
 public string? Command { get; init; }
 public bool Verified { get; init; }
 public string? Source { get; init; }
}

public static class ReclaimRules
{
 public const string Commit = "c154b018c715c38b1362da0ad8c3f016cf2c6b5d";
 public static readonly string[] Variables = ["LOCALAPPDATA","APPDATA","USERPROFILE","TEMP","WINDIR","PROGRAMDATA","SYSTEMDRIVE","STEAM","STEAMLIBRARY"];
 public static IReadOnlyList<ReclaimRule> All { get; } = Load();
 private static IReadOnlyList<ReclaimRule> Load()
 {
  using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("FromZero.Reclaim.targets.json")??throw new InvalidDataException("Temizlik kuralları bulunamadı.");
  var rules=JsonSerializer.Deserialize<List<ReclaimRule>>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Temizlik kuralları okunamadı.");
  using var additions=Assembly.GetExecutingAssembly().GetManifestResourceStream("FromZero.cleanup-additions.json")??throw new InvalidDataException("Additional cleanup rules are missing.");
  rules.AddRange(JsonSerializer.Deserialize<List<ReclaimRule>>(additions,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Additional cleanup rules are invalid."));
  var ids=new HashSet<string>(StringComparer.Ordinal);foreach(var rule in rules){Validate(rule);if(!ids.Add(rule.Id))throw new InvalidDataException("Tekrarlanan temizlik kuralı.");}
  return rules.AsReadOnly();
 }
 public static void Validate(ReclaimRule rule)
 {
  if(!Regex.IsMatch(rule.Id,"^[a-z0-9-]+$")||rule.Mode is not ("contents" or "folder" or "files" or "report")||rule.Risk is not ("safe" or "review" or "caution")||rule.MinAgeHours<0)throw new InvalidDataException("Geçersiz temizlik kuralı.");
  if(rule.Paths.Length==0&&rule.Command==null)throw new InvalidDataException("Kural hedefi eksik.");
  foreach(var path in rule.Paths){
   var parts=path.Split('\\');
   if(!Variables.Any(v=>parts[0].Equals("%"+v+"%",StringComparison.OrdinalIgnoreCase))||parts.Any(p=>p is "" or "." or "..")||path.Contains('/')||path.Contains("**")||parts.Skip(1).Any(p=>p.Contains('%')||p.Contains(':'))||(parts.Length>1&&parts[1].Trim('*','?').Length==0))throw new InvalidDataException("Güvensiz kural yolu: "+path);
   if(rule.Admin&&(parts[0]=="%STEAM%"||parts[0]=="%STEAMLIBRARY%"))throw new InvalidDataException("Yönetici kuralında kullanıcı Steam yolu kullanılamaz.");
  }
  if(rule.Mode=="files"&&rule.Include.Length==0)throw new InvalidDataException("Dosya filtresi eksik.");
  if(rule.Include.Concat(rule.Exclude).Any(p=>string.IsNullOrWhiteSpace(p)||p.IndexOfAny(['\\','/'])>=0||p.Trim('*').Length==0))throw new InvalidDataException("Geçersiz dosya filtresi.");
  if(rule.CloseApps.Any(p=>!p.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||p.IndexOfAny(['\\','/'])>=0))throw new InvalidDataException("Geçersiz uygulama adı.");
 }
 // ReClaim's segment matcher: case insensitive, * and ? never cross a separator.
 public static bool Match(string pattern,string name)
 {
  var p=pattern.ToLowerInvariant();var n=name.ToLowerInvariant();int pi=0,ni=0,star=-1,mark=0;
  while(ni<n.Length){if(pi<p.Length&&(p[pi]=='?'||p[pi]==n[ni])){pi++;ni++;}else if(pi<p.Length&&p[pi]=='*'){star=pi++;mark=ni;}else if(star>=0){pi=star+1;ni=++mark;}else return false;}
  while(pi<p.Length&&p[pi]=='*')pi++;return pi==p.Length;
 }
}
