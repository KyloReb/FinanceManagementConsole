using FMC.Shared.DTOs.Admin;
using System.Text.Json;

namespace FMC.Services;

public class MaintenancePoller : IDisposable
{
    private readonly HttpClient _httpClient;
    private Timer? _timer;

    public MaintenancePoller(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void Start()
    {
        // Poll immediately, then every 30 seconds
        _ = PollAsync();
        _timer = new Timer(async _ => await PollAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private async Task PollAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/system/maintenance?_t={DateTime.UtcNow.Ticks}");
            if (!response.IsSuccessStatusCode) return;
            var json = await response.Content.ReadAsStringAsync();
            var status = JsonSerializer.Deserialize<MaintenanceStatusDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (status != null)
            {
                MaintenanceState.Sync(
                    status.IsActive,
                    status.Message,
                    status.ModeType ?? "full",
                    status.GraceMinutes,
                    status.ScheduledAt,
                    status.ScheduledMessage
                );
            }
        }
        catch
        {
            // Silent - state stays as-is, will retry in 30s
        }
    }

    public void Dispose() => _timer?.Dispose();
}
