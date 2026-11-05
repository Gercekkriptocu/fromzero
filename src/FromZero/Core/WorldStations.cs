namespace FromZero;
public record WorldStation(string Page,string Sign,string Label,string Roof,string Wall,int Sector);
public static class WorldStations
{
 public static IReadOnlyList<WorldStation> All {get;}=[
  new("overview","FROMZERO","Merkez meydanı","#6F9876","#355E51",0),
  new("capsule","FORMAT KIT","Kurtarma deposu","#9A8058","#5D5944",1),
  new("drivers","UPDATE LAB","Güncelleme atölyesi","#739AB0","#355E72",2),
  new("network","WI-FI","Bağlantı istasyonu","#74A7A1","#356964",3),
  new("gaming","ARCADE","Oyun salonu","#A381BA","#5B456D",4),
  new("performance","TURBO","Performans garajı","#C18C56","#69523D",5),
  new("startup","START","Başlangıç terminali","#879FC1","#3D526C",6),
  new("apps","APP SHOP","Uygulama mağazası","#BA92A5","#694B5D",7),
  new("history","ARCHIVE","Kayıt arşivi","#A99877","#615844",8),
  new("cleanup","ZERO CLEAN","Temizlik şirketi","#9BC89C","#416C58",9)
 ];
 public static WorldStation ForPage(string page)=>All.FirstOrDefault(s=>s.Page==(page is "crosshairs" or "valorant-crosshairs" or "benchmark"?"gaming":page=="memory"?"performance":page is "workspace" or "activation"?"history":page))??All[0];
 public static double Door(int sector)=>249+170*sector;
}
