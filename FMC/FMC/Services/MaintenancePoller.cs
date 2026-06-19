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
        _timer = new Timer(async _ => await PollAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private async Task PollAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7026/";
            using var client = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(5) };

            var response = await client.GetAsync($"api/system/maintenance?_t={DateTime.UtcNow.Ticks}");
            Console.WriteLine($"[MAINT-POLLER] GET {baseUrl}api/system/maintenance -> {response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[MAINT-POLLER] Non-success response, body: {await response.Content.ReadAsStringAsync()}");
                return;
            }
            var json = await response.Content.ReadAsStringAsync();
            var status = JsonSerializer.Deserialize<MaintenanceStatusDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Console.WriteLine($"[MAINT-POLLER] Deserialized: IsActive={status?.IsActive} ModeType={status?.ModeType} ScheduledAt={status?.ScheduledAt}");
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
                Console.WriteLine($"[MAINT-POLLER] MaintenanceState synced: IsActive={FMC.Services.MaintenanceState.IsActive}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MAINT-POLLER] Exception: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void Dispose() => _timer?.Dispose();
}
