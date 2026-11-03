using System.IO;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
namespace FromZero;
public record ExecutableImports(string Architecture,List<string> Libraries);
public record DependencyRequirement(string Library,string Architecture,string? Recipe,bool Present,string Evidence,string Source);
public static class ExecutableDependencies
{
 public static ExecutableImports Read(Stream stream)
 {
  using var pe=new PEReader(stream,PEStreamOptions.LeaveOpen);var header=pe.PEHeaders.PEHeader??throw new InvalidDataException("Not a Windows PE image.");
  var architecture=pe.PEHeaders.CoffHeader.Machine switch{Machine.Amd64=>"x64",Machine.I386=>"x86",Machine.Arm64=>"arm64",_=>throw new InvalidDataException("Unsupported executable architecture.")};
  var libraries=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  void AddName(int rva){if(rva<=0)throw new InvalidDataException("Invalid import name address.");var text=pe.GetSectionData(rva).GetReader();var name="";for(var i=0;i<260&&text.RemainingBytes>0;i++){var c=text.ReadByte();if(c==0){if(!Regex.IsMatch(name,@"^[a-zA-Z0-9_.-]{1,250}\.dll$",RegexOptions.IgnoreCase))throw new InvalidDataException("Invalid library name.");libraries.Add(name.ToLowerInvariant());return;}if(c<32||c>126)throw new InvalidDataException("Invalid import name.");name+=(char)c;}throw new InvalidDataException("Unterminated import name.");}
  if(header.ImportTableDirectory.RelativeVirtualAddress>0){var table=pe.GetSectionData(header.ImportTableDirectory.RelativeVirtualAddress).GetReader();bool end=false;for(int i=0;i<4096&&table.RemainingBytes>=20;i++){var a=table.ReadUInt32();var b=table.ReadUInt32();var c=table.ReadUInt32();var name=table.ReadUInt32();var e=table.ReadUInt32();if((a|b|c|name|e)==0){end=true;break;}AddName(checked((int)name));}if(!end)throw new InvalidDataException("Import table limit exceeded.");}
  if(header.DelayImportTableDirectory.RelativeVirtualAddress>0){var table=pe.GetSectionData(header.DelayImportTableDirectory.RelativeVirtualAddress).GetReader();bool end=false;for(int i=0;i<2048&&table.RemainingBytes>=32;i++){var flags=table.ReadUInt32();var name=table.ReadUInt32();ulong any=flags|name;for(int j=0;j<6;j++)any|=table.ReadUInt32();if(any==0){end=true;break;}if((flags&1)==0)throw new InvalidDataException("Legacy delay-import address format requires manual review.");AddName(checked((int)name));}if(!end)throw new InvalidDataException("Delay import table limit exceeded.");}
  return new(architecture,libraries.Order().ToList());
 }
 public static string? Recipe(string library,string arch)
 {
  if(arch is not("x64" or "x86"))return null;library=library.ToLowerInvariant();
  if(Regex.IsMatch(library,@"^(vcruntime140(?:_1)?|msvcp140(?:_\d+)?|concrt140)\.dll$"))return "vc-"+arch;
  foreach(var (suffix,year) in new[]{("80","2005"),("90","2008"),("100","2010"),("110","2012"),("120","2013")})if(library=="msvcr"+suffix+".dll"||library=="msvcp"+suffix+".dll")return "vc"+year+"-"+arch;
  return Regex.IsMatch(library,@"^(d3dx(?:9|10|11)_\d+|d3dcompiler_43|xinput1_3|xaudio2_7|xactengine3_7)\.dll$")?"directx-offline":null;
 }
 public static List<DependencyRequirement> Assess(ExecutableImports imports,string folder,Func<string,bool>? exists=null)
 {
  exists??=File.Exists;var system=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),imports.Architecture=="x86"?"SysWOW64":"System32");
  return imports.Libraries.Select(name=>{var local=Path.Combine(folder,name);var os=Path.Combine(system,name);var bundled=exists(local);var present=bundled||exists(os);var recipe=Recipe(name,imports.Architecture);return new DependencyRequirement(name,imports.Architecture,recipe,present,bundled?"File beside the executable":present?"File in the architecture-specific Windows directory":"Not found in the executable folder or Windows directory",recipe=="directx-offline"?GameComponents.DxPage:recipe!=null?GameComponents.VcPage:"Windows Update / official game or GPU support");}).ToList();
 }
 public static (ExecutableImports Imports,List<DependencyRequirement> Requirements) Inspect(string path)
 {
  if(!Path.IsPathFullyQualified(path)||!File.Exists(path)||new FileInfo(path).Length>2_000_000_000)throw new InvalidDataException("Choose an existing local executable under 2 GB.");
  using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);var imports=Read(stream);return(imports,Assess(imports,Path.GetDirectoryName(path)!));
 }
}
