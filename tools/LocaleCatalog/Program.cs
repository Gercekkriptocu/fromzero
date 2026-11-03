using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
var root=Path.GetFullPath(args[0]);var strings=new Dictionary<string,HashSet<string>>();
foreach(var file in Directory.GetFiles(Path.Combine(root,"src/FromZero"),"*.cs",SearchOption.AllDirectories).Where(p=>!p.Contains("\\obj\\")&&!p.Contains("\\bin\\"))){
 var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(file));
 foreach(var n in tree.GetRoot().DescendantNodes()){
  string? text=n is LiteralExpressionSyntax l&&l.RawKind==(int)SyntaxKind.StringLiteralExpression?l.Token.ValueText:n is InterpolatedStringTextSyntax t?t.TextToken.ValueText:null;
  if(text==null||text.Trim().Length<2||text.StartsWith("http")||text.Contains("Get-CimInstance")||text.Contains("[pscustomobject]")||text.Length>2000)continue;
  if(!Regex.IsMatch(text,"[çğıİöşüÇĞÖŞÜ]")&&!Regex.IsMatch(text,@"(?i)\b(ve|bir|ile|olarak|et|kodu|iade|oldu|kaydet|Yenile|temizle|bul|Onay|tane|iptal|geri|oyun|Lisans|zorunlu|profil|Hata|kopyala|Ayr|Tara|gerek|girdi|klas|bile|yeniden|bekle|sonra|bilin|Eksik|sunucu|dosya|gizli|hesap|devam|kapat|korun|seviye|Ayar|donan|uygul|tespit|yay|sunulan|incele|imza|tarama|Kaynak|durum)"))continue;
  if(!strings.TryGetValue(text,out var paths)){paths=[];strings[text]=paths;}paths.Add(Path.GetFileName(file));
 }
}
var items=strings.OrderByDescending(p=>p.Value.Any(f=>f.EndsWith("Page.cs")||f=="MainWindow.xaml.cs"||f=="Pages.cs"||f=="FpsControls.cs")).ThenBy(p=>p.Key,StringComparer.Ordinal).Select((p,i)=>new{Id=i,Text=p.Key,Files=p.Value}).ToArray();
File.WriteAllText(args.Length>1?Path.GetFullPath(args[1]):Path.Combine(root,"artifacts/locale-strings.json"),JsonSerializer.Serialize(items,new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));Console.WriteLine(items.Length);
