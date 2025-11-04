using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;

namespace LmSecuritySample.Plugins;

public class HardenedBankingTools
{
    private static readonly string Root = Path.GetFullPath("app/data").Replace('\\', '/');

    [KernelFunction, Description("Sicherer Kontostand")]
    public string ViewBalanceSecure([Description("IBAN")] string iban)
    {
        return BankingData.Balances.TryGetValue(iban, out var balance)
            ? $"Kontostand für {iban}: {balance:0.00} EUR"
            : "Konto nicht gefunden";
    }

    [KernelFunction, Description("Sichere Überweisung mit Limits und Bestätigung")]
    public string TransferSecure(
        [Description("Quell IBAN")] string fromIban,
        [Description("Ziel IBAN nur Allowlist")] string toIban,
        [Description("Betrag in Euro als Dezimalzahl")] decimal amount,
        [Description("Verwendungszweck")] string memo,
        [Description("Bestätigung true oder false")] bool confirmed)
    {
        if (!BankingData.Balances.ContainsKey(fromIban))
            return "Quellkonto unbekannt";

        if (!BankingData.Beneficiaries.Contains(toIban))
            return "Empfänger nicht freigeschaltet";

        if (amount <= 0 || amount > 1000)
            return "Betrag außerhalb des zulässigen Rahmens";

        if (!confirmed)
            return "Bestätigung fehlt";

        if (BankingData.Balances[fromIban] < amount)
            return "Nicht genug Guthaben";

        BankingData.Balances[fromIban] -= amount;
        BankingData.Balances[toIban] = BankingData.Balances.GetValueOrDefault(toIban) + amount;

        var tx = new Transaction(fromIban, toIban, amount, memo, DateTime.UtcNow);
        BankingData.Transactions.Add(tx);

        return $"Überweisung sicher ausgeführt. Betrag {amount:0.00} EUR an {toIban}";
    }

    [KernelFunction, Description("CSV Export mit Entschärfung aktiver Formeln")]
    public string ExportTransactionsCsvSecure([Description("IBAN")] string iban)
    {
        var rows = BankingData.Transactions
            .Where(t => t.FromIban.Equals(iban, StringComparison.OrdinalIgnoreCase) || t.ToIban.Equals(iban, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.WhenUtc)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Datum,Von,Nach,Betrag,Verwendungszweck");

        foreach (var r in rows)
        {
            string memo = r.Memo ?? string.Empty;
            if (memo.Length > 0 && "=+-@".IndexOf(memo[0]) >= 0)
                memo = "'" + memo; // entschärfen

            sb.AppendLine($"{r.WhenUtc:O},{r.FromIban},{r.ToIban},{r.Amount:0.00},{memo}");
        }

        return sb.ToString();
    }

    [KernelFunction, Description("Webzugriff mit strenger Allowlist und IP Bereichs Sperre")]
    public string WebFetchSecure([Description("Vollständige HTTPS URL innerhalb bank.example")] string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return "Ungültige URL";

        if (uri.Scheme != Uri.UriSchemeHttps)
            return "Nur HTTPS erlaubt";

        if (!uri.Host.Equals("bank.example", StringComparison.OrdinalIgnoreCase))
            return "Domain nicht erlaubt";

        // Keine echten Requests in der Demo. Liefere statische Inhalte
        return BankingData.Web.GetValueOrDefault("https://bank.example/help/fees", "Kein Inhalt");
    }

    [KernelFunction, Description("Datei lesen mit Kanonisierung und Root Enforcement")]
    public string ReadFileSecure([Description("Relativer Pfad unter app data")] string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine("app/data", relativePath)).Replace('\\', '/');

        if (!full.StartsWith(Root, StringComparison.Ordinal))
            return "Pfad nicht erlaubt";

        return BankingData.Files.TryGetValue(full, out var content)
            ? content
            : "Datei nicht gefunden";
    }
}