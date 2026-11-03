using System.IO;
using System.Net;
using System.Net.Sockets;

namespace FromZero;
public record DnsFamily(string Family, bool Automatic, string[] Servers);
public record DnsAdapter(string Id, string Name, bool Connected, DnsFamily[] Families);
public record DnsProfile(string Id, string Name, string[] V4, string[] V6)
{
 public override string ToString() => Name;
}
public record DnsBackup(DateTimeOffset Created, DnsAdapter Original, DnsFamily[] Applied);
public static class DnsPolicy
{
 public static IReadOnlyList<DnsProfile> Profiles { get; } = [
  new("cloudflare", "Cloudflare", ["1.1.1.1", "1.0.0.1"], ["2606:4700:4700::1111", "2606:4700:4700::1001"]),
  new("google", "Google", ["8.8.8.8", "8.8.4.4"], ["2001:4860:4860::8888", "2001:4860:4860::8844"]),
  new("quad9", "Quad9", ["9.9.9.9", "149.112.112.112"], ["2620:fe::fe", "2620:fe::9"]),
  new("automatic", "Otomatik (modem / ağ)", [], [])
 ];
 public static string Address(string value, string family)
 {
  var type = family == "IPv4" ? AddressFamily.InterNetwork : family == "IPv6" ? AddressFamily.InterNetworkV6 : throw new InvalidDataException("DNS adres ailesi geçersiz.");
  if (!IPAddress.TryParse(value, out var ip) || ip.AddressFamily != type || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6Multicast || (type == AddressFamily.InterNetwork && (ip.GetAddressBytes()[0] >= 224 || ip.GetAddressBytes()[0] == 0))) throw new InvalidDataException("Geçerli bir " + family + " DNS adresi gir.");
  return ip.ToString();
 }
 public static void Validate(DnsAdapter adapter)
 {
  if (!Guid.TryParse(adapter.Id, out _) || adapter.Families.Length is < 1 or > 2 || adapter.Families.Select(f => f.Family).Distinct().Count() != adapter.Families.Length) throw new InvalidDataException("DNS adaptör kaydı geçersiz.");
  foreach (var f in adapter.Families)
  {
   if (f.Family is not "IPv4" and not "IPv6" || f.Servers.Length > 10 || (!f.Automatic && f.Servers.Length == 0)) throw new InvalidDataException("DNS sunucu kaydı geçersiz.");
   foreach (var ip in f.Servers) Address(ip, f.Family);
  }
 }
 public static DnsFamily[] Desired(DnsAdapter adapter, DnsProfile profile)
 {
  Validate(adapter);
  if (!Profiles.Any(p => p.Id == profile.Id)) throw new InvalidDataException("DNS profili desteklenmiyor.");
  // Select only the built-in recipe; caller-supplied addresses cannot replace a profile.
  var recipe = Profiles.Single(p => p.Id == profile.Id);
  return adapter.Families.Select(f => new DnsFamily(f.Family, recipe.Id == "automatic", f.Family == "IPv4" ? recipe.V4 : recipe.V6)).ToArray();
 }
 public static bool Same(DnsFamily a, DnsFamily b) => a.Family == b.Family && a.Automatic == b.Automatic &&
  (a.Automatic || a.Servers.Select(ip => Address(ip, a.Family)).SequenceEqual(b.Servers.Select(ip => Address(ip, b.Family)), StringComparer.OrdinalIgnoreCase));
 public static bool Same(IEnumerable<DnsFamily> a, IEnumerable<DnsFamily> b)
 {
  var left = a.OrderBy(f => f.Family).ToArray(); var right = b.OrderBy(f => f.Family).ToArray();
  return left.Length == right.Length && left.Zip(right).All(pair => Same(pair.First, pair.Second));
 }
}
