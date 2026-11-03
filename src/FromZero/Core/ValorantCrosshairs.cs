using System.Globalization;
using System.IO;
using System.Text.Json;

namespace FromZero;

public sealed record ValorantEntry(string Id,string Name,string Code,string Team,string TeamChecked="");
public sealed record ValorantLines(double Horizontal,double Vertical,double Thickness,double Offset,double Opacity);
public sealed record ValorantCrosshair(uint Rgb,double Outline,double OutlineThickness,double DotSize,double DotOpacity,ValorantLines? Inner,ValorantLines? Outer);

public static class ValorantCrosshairs
{
 public static IReadOnlyList<ValorantEntry> All { get; }=Load();

 private static IReadOnlyList<ValorantEntry> Load()
 {
  using var stream=typeof(ValorantCrosshairs).Assembly.GetManifestResourceStream("FromZero.valorant-crosshairs.json")??throw new InvalidDataException("VALORANT catalogue is missing.");
  var entries=JsonSerializer.Deserialize<List<ValorantEntry>>(stream)??[];
  if(entries.Count!=50||entries.Select(e=>e.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=50||
   entries.Any(e=>string.IsNullOrWhiteSpace(e.Team)||!DateOnly.TryParseExact(e.TeamChecked,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)))
   throw new InvalidDataException("Invalid VALORANT catalogue.");
  foreach(var entry in entries)Decode(entry.Code);
  return entries.AsReadOnly();
 }

 public static ValorantCrosshair Decode(string code)
 {
  const string invalid="Invalid VALORANT crosshair code.";
  if(code.Length is <3 or >600)throw new InvalidDataException(invalid);
  var tokens=code.Split(';');
  if(tokens[0]!="0")throw new InvalidDataException(invalid);
  var values=new Dictionary<string,string>(StringComparer.Ordinal);
  bool primary=false,seenPrimary=false;
  for(int i=1;i<tokens.Length;){
   var key=tokens[i++];
   if(key is "P" or "A" or "S"){
    primary=key=="P";
    if(primary&&seenPrimary)throw new InvalidDataException(invalid);
    seenPrimary|=primary;
    continue;
   }
   if(i==tokens.Length||key.Length is <1 or >3||!key.All(char.IsAsciiLetterOrDigit))throw new InvalidDataException(invalid);
   var value=tokens[i++];
   if(value.Length is <1 or >12||!value.All(c=>char.IsAsciiLetterOrDigit(c)||c=='.'||c=='-'))throw new InvalidDataException(invalid);
   if(primary&&!values.TryAdd(key,value))throw new InvalidDataException(invalid);
  }
  if(!seenPrimary)throw new InvalidDataException(invalid);
  double Read(string key,double fallback,double max)
  {
   if(!values.TryGetValue(key,out var raw))return fallback;
   if(!double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out var result)||!double.IsFinite(result))throw new InvalidDataException(invalid);
   return Math.Clamp(result,0,max);
  }
  uint[] palette=[0xffffff,0x00ff00,0x7fff00,0xccff00,0xffff00,0x00ffff,0xff00ff,0xff0000];
  var index=(int)Read("c",0,8);
  uint color=0xffffff;
  if(index<palette.Length)color=palette[index];
  else if(values.TryGetValue("u",out var rgba)&&(rgba.Length is 6 or 8)&&uint.TryParse(rgba,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var parsed))color=rgba.Length==8?parsed>>8:parsed;
  ValorantLines? Lines(string prefix,double length,double opacity)
  {
   if(Read(prefix+"b",1,1)==0)return null;
   var horizontal=Read(prefix+"l",length,20);
   return new(horizontal,Read(prefix+"g",0,1)>0?Read(prefix+"v",horizontal,20):horizontal,Read(prefix+"t",2,10),Read(prefix+"o",prefix=="0"?3:10,40),Read(prefix+"a",opacity,1));
  }
  return new(color,Read("h",1,1)>0?Read("o",.5,1):0,Read("t",1,6),Read("d",0,1)>0?Read("z",2,10):0,Read("a",1,1),Lines("0",6,.8),Lines("1",2,.35));
 }
}
