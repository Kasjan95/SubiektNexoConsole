namespace SubiektNexoConnector.Deployment.Testing;

public sealed record PostRebootResultPresentation(
    bool IsSuccess,
    string Status,
    string Heading,
    string Summary,
    string VerifiedAt,
    string WindowsStartedAt,
    string Details,
    string Recommendation,
    string ReportPath)
{
    public static PostRebootResultPresentation Create(string instanceName, string reportPath, string? report)
    {
        if (string.IsNullOrWhiteSpace(report))
        {
            return new(false, "BRAK WYNIKU", "Nie udało się potwierdzić działania adaptera",
                $"Test instancji „{instanceName}” nie zapisał wyniku w ciągu 7 minut.",
                "—", "—", "Weryfikator nie utworzył raportu.",
                "Otwórz instalator, wybierz tę instancję i użyj „Sprawdź działanie”. Jeśli test nadal nie przejdzie, zapisz diagnostykę.",
                reportPath);
        }

        var success = report.StartsWith("TEST PO RESTARCIE: SUKCES", StringComparison.Ordinal);
        var verifiedAt = ReadValue(report, "Czas:") ?? "—";
        var windowsStartedAt = ReadValue(report, "Szacowany start Windows:") ?? "—";
        if (success)
        {
            var warehouses = ReadValue(report, "Liczba magazynów:");
            var warehouseText = warehouses is null ? "" : $" Odczytano magazyny: {warehouses}.";
            return new(true, "TEST ZAKOŃCZONY", "Adapter działa po restarcie",
                $"Instancja „{instanceName}” odpowiedziała na /health oraz na autoryzowane /warehouses.{warehouseText}",
                verifiedAt, windowsStartedAt, report,
                "Nie musisz nic robić. Test potwierdza dostępność adaptera po restarcie; nie dowodzi jednak samodzielnie, że proces działał jeszcze przed pierwszym logowaniem użytkownika.",
                reportPath);
        }

        return new(false, "WYMAGA UWAGI", "Adapter nie przeszedł testu po restarcie",
            $"Nie udało się potwierdzić działania instancji „{instanceName}”. Konfiguracja nie została przez ten test zmieniona.",
            verifiedAt, windowsStartedAt, report,
            "Uruchom instalator, wybierz tę instancję i użyj „Sprawdź działanie”. Jeżeli błąd się powtórzy, zapisz diagnostykę — szczegóły techniczne są też w raporcie poniżej.",
            reportPath);
    }

    private static string? ReadValue(string report, string label)
    {
        foreach (var line in report.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(label, StringComparison.Ordinal))
                return line[label.Length..].Trim();
        }
        return null;
    }
}
