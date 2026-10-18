using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11EasyConfig.Core.Abstractions;
using Win11EasyConfig.Core.Features;
using Win11EasyConfig.Core.Services;

namespace Win11EasyConfig.App.ViewModels;

/// <summary>Read-only summary of the machine (OS + 硬件), shown on the 系统信息 page.</summary>
public sealed partial class SystemInfoViewModel : ObservableObject
{
    private readonly WindowsInfoService _windowsInfo;
    private readonly ICommandRunner _commandRunner;
    private readonly HardwareInfoService _hardware;

    [ObservableProperty]
    public partial string ProductName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DisplayVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BuildNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditionId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MachineName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProcessorCount { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ElevationStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SystemDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProcessorName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MemorySummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DiskSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoadingHardware { get; set; }

    public SystemInfoViewModel(
        WindowsInfoService windowsInfo,
        ICommandRunner commandRunner,
        HardwareInfoService hardware)
    {
        _windowsInfo = windowsInfo;
        _commandRunner = commandRunner;
        _hardware = hardware;
    }

    public ObservableCollection<string> MemorySticks { get; } = [];

    public ObservableCollection<string> Gpus { get; } = [];

    public ObservableCollection<string> Disks { get; } = [];

    [RelayCommand]
    public async Task LoadAsync()
    {
        var version = _windowsInfo.GetVersion();

        ProductName = string.IsNullOrWhiteSpace(version.ProductName) ? "Windows" : version.ProductName;
        DisplayVersion = string.IsNullOrWhiteSpace(version.DisplayVersion) ? "-" : version.DisplayVersion;
        BuildNumber = version.FullBuild;
        EditionId = string.IsNullOrWhiteSpace(version.EditionId) ? "-" : version.EditionId;
        MachineName = Environment.MachineName;
        ProcessorCount = $"{Environment.ProcessorCount} 逻辑处理器";
        ElevationStatus = WindowsInfoService.IsElevated() ? "已以管理员身份运行" : "未提权（部分功能不可用）";
        SystemDirectory = Environment.SystemDirectory;

        await LoadHardwareAsync().ConfigureAwait(true);
    }

    private async Task LoadHardwareAsync()
    {
        IsLoadingHardware = true;
        try
        {
            // WMI queries can take a moment, so keep them off the UI thread.
            var info = await Task.Run(_hardware.GetHardwareInfo).ConfigureAwait(true);

            ProcessorName = info.ProcessorName;
            MemorySummary = $"共 {info.TotalMemoryGb:0.#} GB，{info.MemorySticks.Count} 根内存条";
            DiskSummary = $"共 {info.Disks.Count} 块硬盘，总容量约 {info.TotalDiskGb:0} GB";

            MemorySticks.Clear();
            foreach (var stick in info.MemorySticks)
            {
                MemorySticks.Add($"{stick.CapacityGb:0.#} GB · {stick.Manufacturer}");
            }

            Gpus.Clear();
            foreach (var gpu in info.Gpus)
            {
                Gpus.Add($"{gpu.Name}（驱动 {gpu.DriverVersion}）");
            }

            Disks.Clear();
            foreach (var disk in info.Disks)
            {
                Disks.Add($"{disk.Model} · {disk.CapacityGb:0} GB");
            }
        }
        finally
        {
            IsLoadingHardware = false;
        }
    }

    [RelayCommand]
    private void OpenWindowsSettings() => _commandRunner.Launch("ms-settings:about");
}
