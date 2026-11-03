using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FromZero;
public static class Locale
{
 private sealed class Node { public readonly Dictionary<char,Node> Children=[]; public string? Value;public string Key=""; }
 private static readonly Dictionary<string,string> English=Load();
 private static readonly Dictionary<string,string> Turkish=English.Where(p=>p.Value!=p.Key).GroupBy(p=>p.Value,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First().Key,StringComparer.Ordinal);
 private static readonly Node EnTrie=Trie(English),TrTrie=Trie(Turkish);
 public static string Language {get;private set;}=ReadLanguage();
 public static bool IsTurkish=>Language=="tr";
 public static CultureInfo Culture=>CultureInfo.GetCultureInfo(IsTurkish?"tr-TR":"en-US");
 public static event Action? Changed;
 private static Dictionary<string,string> Load(){using var stream=typeof(Locale).Assembly.GetManifestResourceStream("FromZero.locale-en.json");return stream==null?[]:JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??[];}
 private static string ReadLanguage()=>ReadLanguage(LocalStore.FilePath("language.json"),
  Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","install-language.txt")));
 internal static string ReadLanguage(string preferences,string installer)
 {
  try{
   if(File.Exists(preferences)&&new FileInfo(preferences).Length<=256){
    var saved=JsonSerializer.Deserialize<string>(File.ReadAllText(preferences));
    if(saved is "tr" or "en")return saved;
   }
  }catch(Exception ex)when(ex is IOException or JsonException or UnauthorizedAccessException){}
  try{return File.Exists(installer)&&new FileInfo(installer).Length<=16&&File.ReadAllText(installer).Trim()=="tr"?"tr":"en";}
  catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){return "en";}
 }
 public static void Set(string language,bool persist=true){if(language is not "en" and not "tr")throw new InvalidDataException("Supported languages: en, tr.");if(persist)File.WriteAllText(LocalStore.FilePath("language.json"),JsonSerializer.Serialize(language));Language=language;CultureInfo.CurrentCulture=Culture;CultureInfo.CurrentUICulture=Culture;Changed?.Invoke();}
 private static Node Trie(Dictionary<string,string> map){var root=new Node();foreach(var entry in map.Where(p=>p.Key.Length>0)){var current=root;foreach(var c in entry.Key){if(!current.Children.TryGetValue(c,out var next)){next=new();current.Children[c]=next;}current=next;}current.Key=entry.Key;current.Value=entry.Value;}return root;}
 public static string Text(string source)=>Translate(source,IsTurkish);
 public static string Translate(string source,bool turkish)
 {
  var map=turkish?Turkish:English;if(map.TryGetValue(source,out var exact))return exact;
  var trim=source.Trim();if(map.TryGetValue(trim,out exact)){var start=source.IndexOf(trim,StringComparison.Ordinal);return source[..start]+exact+source[(start+trim.Length)..];}
  var root=turkish?TrTrie:EnTrie;var result=new StringBuilder();
  for(int i=0;i<source.Length;){var current=root;string? replacement=null;int length=0;
   for(int j=i;j<source.Length&&current.Children.TryGetValue(source[j],out current!);j++){
    if(current.Value==null)continue;
    bool boundary=(i==0||!char.IsLetterOrDigit(source[i-1])||!char.IsLetterOrDigit(source[i]))&&(j+1==source.Length||!char.IsLetterOrDigit(source[j+1])||!char.IsLetterOrDigit(source[j]));
    if(boundary){replacement=current.Value;length=j-i+1;}
   }
   if(replacement!=null){result.Append(replacement);i+=length;}else result.Append(source[i++]);
  }
  return result.ToString();
 }
 public static int EntryCount=>English.Count;
}
