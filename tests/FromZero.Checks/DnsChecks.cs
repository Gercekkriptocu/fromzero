using System.Text;
using FromZero;

public static class DnsChecks
{
 public static async Task Run(Action<bool, string> check)
 {
  var original = new DnsAdapter(Guid.NewGuid().ToString(), "Wi-Fi", true, [new("IPv4", true, ["192.168.1.1"]), new("IPv6", false, ["::1"])]);
  DnsPolicy.Validate(original);
  check(DnsPolicy.Profiles.Count == 4 && DnsPolicy.Profiles.Where(p => p.Id != "automatic").All(p => p.V4.Length == 2 && p.V6.Length == 2), "DNS profiles contain primary and secondary IPv4 and IPv6 servers");
  var wanted = DnsPolicy.Desired(original, DnsPolicy.Profiles[0]);
  check(wanted[0].Servers.SequenceEqual(["1.1.1.1", "1.0.0.1"]) && wanted[1].Servers[0] == "2606:4700:4700::1111", "DNS Cloudflare recipe is complete for both address families");
  check(DnsPolicy.Desired(original, new("cloudflare", "spoof", ["8.8.8.8"], [])).First().Servers[0] == "1.1.1.1", "DNS profiles cannot substitute caller-supplied server addresses");
  check(DnsPolicy.Same(original.Families[0], new("IPv4", true, ["192.168.2.1"])) && !DnsPolicy.Same(original.Families[1], new("IPv6", false, ["::2"])), "DNS automatic mode tolerates DHCP lease changes while static servers match exactly");
  var rejected = false; try { DnsPolicy.Address("1.1.1.1; Start-Process bad", "IPv4"); } catch (System.IO.InvalidDataException) { rejected = true; }
  check(rejected, "DNS address validation rejects shell expressions");
  rejected = false; try { DnsPolicy.Address("8.8.8.8", "IPv6"); } catch (System.IO.InvalidDataException) { rejected = true; }
  check(rejected, "DNS rejects mismatched IP address families");
  check(DnsPolicy.Address("::1", "IPv6") == "::1", "DNS backup can preserve a local resolver");
  var backend = new FakeDnsBackend(original); var service = new DnsService(backend);
  await service.ApplyAsync(original.Id, "cloudflare");
  check(service.HasBackup(original.Id) && DnsPolicy.Same(backend.Current.Families, wanted), "DNS saves original preferences before applying the profile");
  await service.ApplyAsync(original.Id, "google"); await service.RestoreAsync(original.Id);
  check(DnsPolicy.Same(backend.Current.Families, original.Families) && !service.HasBackup(original.Id), "DNS provider switching preserves and restores the first mixed automatic/static baseline");
  await service.ApplyAsync(original.Id, "automatic");
  check(backend.Current.Families.All(f => f.Automatic), "DNS automatic profile resets both configured address families");
  await service.RestoreAsync(original.Id);
  var calls = backend.Calls; await service.ApplyAsync(original.Id, "cloudflare"); calls = backend.Calls;
  await service.ApplyAsync(original.Id, "cloudflare");
  check(backend.Calls == calls, "DNS identical profiles cause no redundant elevation or write");
  backend.Current = backend.Current with { Families = [new("IPv4", false, ["10.0.0.53"]), wanted[1]] };
  rejected = false; try { await service.RestoreAsync(original.Id); } catch (InvalidOperationException) { rejected = true; }
  check(rejected && service.HasBackup(original.Id) && backend.Calls == calls, "DNS restore preserves external or VPN changes and keeps recovery backup");
  rejected = false; try { await service.ApplyAsync(original.Id, "quad9"); } catch (InvalidOperationException) { rejected = true; }
  check(rejected && backend.Calls == calls, "DNS profile switching refuses to overwrite an external change");
  backend.Current = backend.Current with { Families = wanted }; await service.RestoreAsync(original.Id);
  backend.Fail = true;
  rejected = false; try { await service.ApplyAsync(original.Id, "google"); } catch (InvalidOperationException) { rejected = true; }
  check(rejected && !service.HasBackup(original.Id) && DnsPolicy.Same(backend.Current.Families, original.Families), "DNS canceled elevation or native rollback leaves original preferences and no false active profile");
  backend.Fail = false; await service.ApplyAsync(original.Id, "cloudflare"); backend.Fail = true;
  try { await service.ApplyAsync(original.Id, "google"); } catch (InvalidOperationException) { }
  backend.Fail = false; await service.RestoreAsync(original.Id);
  check(DnsPolicy.Same(backend.Current.Families, original.Families), "DNS failed second profile keeps the original recovery point");
  backend.Current = original with { Id = Guid.NewGuid().ToString(), Families = [original.Families[0]] };
  await service.ApplyAsync(backend.Current.Id, "quad9");
  check(backend.Current.Families.Length == 1 && backend.Current.Families[0].Servers[0] == "9.9.9.9", "DNS respects adapters with only one available address family");
  await service.RestoreAsync(backend.Current.Id);
  var script = WindowsDnsBackend.Script(original, wanted);
  var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(script));
  var errors = await Commands.JsonAsync<string[]>("$source=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + encoded + "'));$tokens=$null;$parseErrors=$null;[void][System.Management.Automation.Language.Parser]::ParseInput($source,[ref]$tokens,[ref]$parseErrors);ConvertTo-Json -InputObject @($parseErrors | ForEach-Object {$_.Message})");
  check(errors.Length == 0 && script.Contains("Get-NetAdapter -Physical") && script.Contains("$touched") && script.Contains("Set-DnsClientServerAddress -ResetServerAddresses"), "DNS generated transaction parses in Windows PowerShell and includes identity guards and rollback");
  var native = await new WindowsDnsBackend().ReadAsync();
  check(native.All(a => Guid.TryParse(a.Id, out _) && a.Families.Length > 0), "DNS native read-only adapter inventory and static/automatic configuration can be read without changing network settings");
 }
 private sealed class FakeDnsBackend(DnsAdapter original) : IDnsBackend
 {
  public DnsAdapter Current = original; public int Calls; public bool Fail;
  public Task<IReadOnlyList<DnsAdapter>> ReadAsync() => Task.FromResult<IReadOnlyList<DnsAdapter>>([Current]);
  public Task SetAsync(DnsAdapter expected, DnsFamily[] desired)
  {
   Calls++; if (!DnsPolicy.Same(Current.Families, expected.Families)) throw new InvalidOperationException("changed");
   if (Fail) throw new InvalidOperationException("canceled/rolled back"); Current = Current with { Families = desired }; return Task.CompletedTask;
  }
 }
}
