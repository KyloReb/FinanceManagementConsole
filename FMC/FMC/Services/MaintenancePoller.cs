using FMC.Shared.DTOs.Admin;
using System.Text.Json;

namespace FMC.Services;

public class MaintenancePoller : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private Timer? _timer;

    public MaintenancePoller(IServiceScopeFactory scopeFactory, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    public void Start()
    {
        _ = PollAsync();
        _timer = new Timer(async _ => await PollAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private async Task PollAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7026/";
            using var client = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };

            var response = await client.GetAsync($"api/system/maintenance?_t={DateTime.UtcNow.Ticks}");
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
        catch { }
    }

    public void Dispose() => _timer?.Dispose();
}
