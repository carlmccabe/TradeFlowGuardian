using Npgsql;

namespace TradeFlowGuardian.Infrastructure.Data;

public static class PostgresConnectionHelper
{
    /// <summary>
    /// Converts Railway's postgresql://user:pass@host:port/db URI format to the
    /// Npgsql key=value connection string format. Pass-through for strings that are
    /// already in key=value format.
    /// </summary>
    public static string Normalize(string? connectionString)
    {
        connectionString = connectionString?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(connectionString))
            return string.Empty;

        if (!connectionString.Contains("://", StringComparison.Ordinal))
            return connectionString;

        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
            return string.Empty;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host     = uri.Host,
            Port     = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]),
            Password = uri.UserInfo.Contains(':')
                ? Uri.UnescapeDataString(uri.UserInfo.Split(':', 2)[1])
                : string.Empty,
            SslMode  = SslMode.Require,
        };

        return builder.ConnectionString;
    }
}
