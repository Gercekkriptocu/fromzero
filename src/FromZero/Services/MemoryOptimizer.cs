using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace FromZero;

public interface IMemoryBackend
{
 MemorySnapshot Read();
 IReadOnlyList<MemoryTarget> Applications();
 MemoryItemResult Trim(MemoryTarget target);
}
public sealed class MemoryOptimizer(IMemoryBackend? backend = null)
{
 private readonly IMemoryBackend backend = backend ?? new WindowsMemoryBackend();
 private readonly SemaphoreSlim gate = new(1, 1);
 public MemorySnapshot Read() => backend.Read();
 public IReadOnlyList<MemoryTarget> Applications() => backend.Applications();
 public async Task<MemoryReport> OptimizeAsync(IEnumerable<MemoryTarget> targets, string mode)
 {
  if (!await gate.WaitAsync(0)) throw new InvalidOperationException("Bir RAM işlemi zaten sürüyor.");
  try
  {
   var requested = targets.Where(t => t.Eligible).DistinctBy(t => (t.Id, t.Started)).Take(100).ToArray();
   return await Task.Run(async () =>
   {
    var before = backend.Read();
    var items = new List<MemoryItemResult>();
    foreach (var target in requested) items.Add(backend.Trim(target));
    await Task.Delay(750);
    return new MemoryReport(DateTimeOffset.Now, mode, before, backend.Read(), items);
   });
  }
  finally { gate.Release(); }
 }
 public static MemoryOptions LoadOptions()
 {
  try { var path = LocalStore.FilePath("memory-options.json"); var options = File.Exists(path) ? JsonSerializer.Deserialize<MemoryOptions>(File.ReadAllText(path), LocalStore.Json) ?? new() : new(); options.Validate(); return options; }
  catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { Journal.Write("RAM tercihleri", ex.Message, false); return new(); }
 }
 public static void SaveOptions(MemoryOptions options)
 {
  options.Validate(); var path = LocalStore.FilePath("memory-options.json");
  File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(options, LocalStore.Json)); File.Move(path + ".tmp", path, true);
 }
}

public sealed class WindowsMemoryBackend : IMemoryBackend
{
 [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus { public uint Length, Load; public ulong Total, Available, PageTotal, PageAvailable, VirtualTotal, VirtualAvailable, Extended; }
 [StructLayout(LayoutKind.Sequential)] private struct PerformanceInfo { public uint Size; public nuint Commit, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable, SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize; public uint Handles, Processes, Threads; }
 [StructLayout(LayoutKind.Sequential)] private struct ProcessMemory { public uint Size, Faults; public nuint PeakWorkingSet, WorkingSet, PeakPaged, Paged, PeakNonpaged, Nonpaged, Pagefile, PeakPagefile; }
 [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
 [DllImport("psapi.dll", SetLastError = true)] private static extern bool GetPerformanceInfo(out PerformanceInfo info, uint size);
 [DllImport("psapi.dll", SetLastError = true)] private static extern bool GetProcessMemoryInfo(SafeProcessHandle process, out ProcessMemory memory, uint size);
 [DllImport("psapi.dll", SetLastError = true)] private static extern bool EmptyWorkingSet(SafeProcessHandle process);
 [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int id);
 [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);
 [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref uint length);
 [DllImport("kernel32.dll", SetLastError = true)] private static extern bool IsProcessCritical(SafeProcessHandle process, out bool critical);
 [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
 [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int type, IntPtr info, uint size, out uint needed);
 [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
 private static readonly string UserSid = WindowsIdentity.GetCurrent().User!.Value;
 private static readonly int Session = CurrentSession();
 private static int CurrentSession() { using var p = Process.GetCurrentProcess(); return p.SessionId; }
 private static ulong WorkingSet(SafeProcessHandle handle)
 {
  if (!GetProcessMemoryInfo(handle, out var memory, (uint)Marshal.SizeOf<ProcessMemory>())) throw new Win32Exception(Marshal.GetLastWin32Error());
  return (ulong)memory.WorkingSet;
 }
 private static bool SameUser(SafeProcessHandle handle)
 {
  if (!OpenProcessToken(handle, 8, out var token)) return false;
  using (token)
  {
   GetTokenInformation(token, 1, IntPtr.Zero, 0, out var size);
   if (size == 0 || size > 65536) return false;
   var buffer = Marshal.AllocHGlobal((int)size);
   try { return GetTokenInformation(token, 1, buffer, size, out _) && new SecurityIdentifier(Marshal.ReadIntPtr(buffer)).Value == UserSid; }
   finally { Marshal.FreeHGlobal(buffer); }
  }
 }
 public MemorySnapshot Read()
 {
  var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
  if (!GlobalMemoryStatusEx(ref status)) throw new Win32Exception(Marshal.GetLastWin32Error());
  if (!GetPerformanceInfo(out var info, (uint)Marshal.SizeOf<PerformanceInfo>())) throw new Win32Exception(Marshal.GetLastWin32Error());
  return new(DateTimeOffset.Now, status.Total, status.Available, (ulong)info.Commit * (ulong)info.PageSize, (ulong)info.CommitLimit * (ulong)info.PageSize);
 }
 private static string[] Games() => GameService.ReadProfiles().Select(p => p.ProcessName).Where(n => n.Length > 0).ToArray();
 private static MemoryTarget? Inspect(Process process, SafeProcessHandle handle, string[] games)
 {
  if (process.SessionId != Session || !SameUser(handle)) return null;
  var window = process.MainWindowHandle;
  if (window == IntPtr.Zero && process.Id != Environment.ProcessId) return null;
  if (!GetProcessTimes(handle, out var started, out _, out _, out _)) return null;
  var path = new StringBuilder(32768); uint length = (uint)path.Capacity;
  if (!QueryFullProcessImageName(handle, 0, path, ref length)) return null;
  var name = System.IO.Path.GetFileNameWithoutExtension(path.ToString());
  var workingSet = WorkingSet(handle);
  GetWindowThreadProcessId(GetForegroundWindow(), out var foreground);
  var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
  var reason = !IsProcessCritical(handle, out var critical) || critical ? "Korumalı işlem" :
   path.ToString().StartsWith(windows, StringComparison.OrdinalIgnoreCase) || MemoryPolicy.ProtectedName(name, games) ? "Sistem / oyun korunuyor" :
   process.Id != Environment.ProcessId && foreground == process.Id ? "Ön planda" :
   process.Id != Environment.ProcessId && !IsIconic(window) ? "Önce pencereyi küçült" :
   workingSet < MemoryPolicy.MinimumWorkingSet ? "32 MB altında" : "Hazır";
  return new(process.Id, started, name, path.ToString(), workingSet, reason == "Hazır", reason);
 }
 public IReadOnlyList<MemoryTarget> Applications()
 {
  var result = new List<MemoryTarget>(); var games = Games();
  foreach (var process in Process.GetProcesses())
  using (process)
  {
   try { using var handle = OpenProcess(0x1000, false, process.Id); if (handle.IsInvalid) continue; var target = Inspect(process, handle, games); if (target != null) result.Add(target); }
   catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or IOException) { }
  }
  return result.OrderByDescending(p => p.WorkingSet).ToArray();
 }
 public MemoryItemResult Trim(MemoryTarget target)
 {
  try
  {
   using var process = Process.GetProcessById(target.Id);
   // The same handle is used for identity checks and EmptyWorkingSet; reused PIDs are rejected.
   using var handle = OpenProcess(0x1000 | 0x100, false, target.Id);
   if (handle.IsInvalid) return new(target.Name, "Erişim yok; atlandı");
   var current = Inspect(process, handle, Games());
   if (current == null || current.Started != target.Started || !current.Path.Equals(target.Path, StringComparison.OrdinalIgnoreCase)) return new(target.Name, "İşlem değişti; atlandı");
   if (!current.Eligible) return new(target.Name, current.Reason + "; atlandı");
   if (!EmptyWorkingSet(handle)) return new(target.Name, "Windows reddetti (" + Marshal.GetLastWin32Error() + ")");
   return new(target.Name, "Uygulandı", current.WorkingSet, WorkingSet(handle));
  }
  catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or IOException) { return new(target.Name, "İşlem kapandı / erişilemiyor; atlandı"); }
 }
}
