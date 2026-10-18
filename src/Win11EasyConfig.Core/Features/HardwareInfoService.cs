using System.Management;
using System.Runtime.Versioning;

namespace Win11EasyConfig.Core.Features;

public sealed record MemoryStick(double CapacityGb, string Manufacturer);

public sealed record GpuInfo(string Name, string DriverVersion);

public sealed record DiskInfo(string Model, double CapacityGb);

public sealed record HardwareInfo(
    string ProcessorName,
    IReadOnlyList<MemoryStick> MemorySticks,
    double TotalMemoryGb,
    IReadOnlyList<GpuInfo> Gpus,
    IReadOnlyList<DiskInfo> Disks,
    double TotalDiskGb);

/// <summary>
/// Hardware inventory via WMI (CPU, memory sticks, GPUs, disks).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class HardwareInfoService
{
    public HardwareInfo GetHardwareInfo()
    {
        return new HardwareInfo(
            ProcessorName: QueryProcessor(),
            MemorySticks: QueryMemory(out var totalMemory),
            TotalMemoryGb: totalMemory,
            Gpus: QueryGpus(),
            Disks: QueryDisks(out var totalDisk),
            TotalDiskGb: totalDisk);
    }

    private static string QueryProcessor()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var item in searcher.Get())
            {
                return item["Name"]?.ToString()?.Trim() ?? "未知";
            }
        }
        catch
        {
            // WMI can be unavailable in restricted environments.
        }

        return "未知";
    }

    private static IReadOnlyList<MemoryStick> QueryMemory(out double totalGb)
    {
        var sticks = new List<MemoryStick>();
        totalGb = 0;

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity, Manufacturer FROM Win32_PhysicalMemory");
            foreach (var item in searcher.Get())
            {
                var capacityGb = 0.0;
                if (item["Capacity"] is not null && long.TryParse(item["Capacity"].ToString(), out var bytes))
                {
                    capacityGb = Math.Round(bytes / 1024.0 / 1024.0 / 1024.0, 1);
                }

                var manufacturer = item["Manufacturer"]?.ToString()?.Trim();
                sticks.Add(new MemoryStick(capacityGb, string.IsNullOrWhiteSpace(manufacturer) ? "未知" : manufacturer));
                totalGb += capacityGb;
            }
        }
        catch
        {
        }

        totalGb = Math.Round(totalGb, 1);
        return sticks;
    }

    private static IReadOnlyList<GpuInfo> QueryGpus()
    {
        var gpus = new List<GpuInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController");
            foreach (var item in searcher.Get())
            {
                var name = item["Name"]?.ToString()?.Trim() ?? "未知";
                var driver = item["DriverVersion"]?.ToString()?.Trim() ?? "-";
                gpus.Add(new GpuInfo(name, driver));
            }
        }
        catch
        {
        }

        return gpus;
    }

    private static IReadOnlyList<DiskInfo> QueryDisks(out double totalGb)
    {
        var disks = new List<DiskInfo>();
        totalGb = 0;

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Model, Size FROM Win32_DiskDrive");
            foreach (var item in searcher.Get())
            {
                var model = item["Model"]?.ToString()?.Trim() ?? "未知";
                var capacityGb = 0.0;
                if (item["Size"] is not null && double.TryParse(item["Size"].ToString(), out var bytes))
                {
                    capacityGb = Math.Round(bytes / 1024.0 / 1024.0 / 1024.0, 0);
                }

                disks.Add(new DiskInfo(model, capacityGb));
                totalGb += capacityGb;
            }
        }
        catch
        {
        }

        totalGb = Math.Round(totalGb, 0);
        return disks;
    }
}
