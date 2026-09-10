using SubiektNexoConnector.Deployment.Testing;

namespace SubiektNexoConnector.Deployment.Tests.Testing;

public sealed class PostRebootResultPresentationTests
{
    [Fact]
    public void Create_PresentsSuccessfulChecksAndTimestamps()
    {
        var report = """
            TEST PO RESTARCIE: SUKCES
            Czas: 2026-09-09 08:15:20 +02:00
            Instancja: pilot-01
            Szacowany start Windows: 2026-09-09T08:14:01.0000000+02:00
            GET /health: OK
            GET /warehouses + API key + Sfera: OK
            Liczba magazynów: 3
            """;

        var result = PostRebootResultPresentation.Create("pilot-01", @"C:\report.txt", report);

        Assert.True(result.IsSuccess);
        Assert.Equal("TEST ZAKOŃCZONY", result.Status);
        Assert.Contains("/health", result.Summary);
        Assert.Contains("3", result.Summary);
        Assert.Equal("2026-09-09 08:15:20 +02:00", result.VerifiedAt);
        Assert.Equal("2026-09-09T08:14:01.0000000+02:00", result.WindowsStartedAt);
    }

    [Fact]
    public void Create_PresentsFailureWithRecoveryInstruction()
    {
        var report = """
            TEST PO RESTARCIE: BŁĄD
            Czas: 2026-09-09 08:15:20 +02:00
            Instancja: pilot-01
            Błąd: HttpRequestException (0x80131500)
            Szczegóły: Połączenie zostało odrzucone.
            """;

        var result = PostRebootResultPresentation.Create("pilot-01", @"C:\report.txt", report);

        Assert.False(result.IsSuccess);
        Assert.Equal("WYMAGA UWAGI", result.Status);
        Assert.Contains("Sprawdź działanie", result.Recommendation);
        Assert.Contains("Połączenie zostało odrzucone", result.Details);
    }

    [Fact]
    public void Create_PresentsMissingReportAsTimeout()
    {
        var result = PostRebootResultPresentation.Create("pilot-01", @"C:\report.txt", null);

        Assert.False(result.IsSuccess);
        Assert.Equal("BRAK WYNIKU", result.Status);
        Assert.Contains("7 minut", result.Summary);
        Assert.Equal("—", result.VerifiedAt);
    }
}
