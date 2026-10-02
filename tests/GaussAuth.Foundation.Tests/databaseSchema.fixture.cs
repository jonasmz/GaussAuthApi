using Npgsql;

namespace GaussAuth.Foundation.Tests;

/// <summary>Reads a PostgreSQL schema into comparable text so tests can assert structure without hard-coding all of it.</summary>
internal static class DatabaseSchema
{
    /// <summary>One sorted line per column, index and constraint of the public schema (plus the migration history table).</summary>
    public static async Task<IReadOnlyList<string>> DescribeAsync(string connectionString)
    {
        var lines = new List<string>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        lines.AddRange(await QueryAsync(connection, """
            SELECT 'column ' || table_name || '.' || column_name || ' ' || data_type || ' nullable=' || is_nullable
                   || ' length=' || coalesce(character_maximum_length::text, '')
            FROM information_schema.columns WHERE table_schema = 'public'
            """));
        lines.AddRange(await QueryAsync(connection, """
            SELECT 'index ' || tablename || ' ' || indexname || ' ' || indexdef FROM pg_indexes WHERE schemaname = 'public'
            """));
        lines.AddRange(await QueryAsync(connection, """
            SELECT 'constraint ' || conrelid::regclass::text || ' ' || conname || ' ' || pg_get_constraintdef(oid)
            FROM pg_constraint WHERE connamespace = 'public'::regnamespace
            """));

        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    /// <summary>Unique indexes (including primary keys and unique constraints) as <c>Table(Column,Column)</c>.</summary>
    public static async Task<IReadOnlySet<string>> UniqueIndexesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var rows = await QueryAsync(connection, """
            SELECT t.relname || '(' || string_agg(a.attname, ',' ORDER BY k.ord) || ')'
            FROM pg_index ix
            JOIN pg_class t ON t.oid = ix.indrelid
            JOIN LATERAL unnest(ix.indkey) WITH ORDINALITY AS k(attnum, ord) ON true
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE ix.indisunique AND t.relnamespace = 'public'::regnamespace
            GROUP BY t.relname, ix.indexrelid
            """);
        return rows.ToHashSet(StringComparer.Ordinal);
    }

    public static async Task<IReadOnlySet<string>> IndexNamesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return (await QueryAsync(connection, "SELECT indexname FROM pg_indexes WHERE schemaname = 'public'")).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Number of foreign keys on a table whose key spans exactly <paramref name="columnCount"/> columns.</summary>
    public static async Task<int> ForeignKeysWithColumnCountAsync(string connectionString, string table, int columnCount)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM pg_constraint WHERE contype = 'f' AND conrelid = @table::regclass AND cardinality(conkey) = @count";
        command.Parameters.AddWithValue("table", $"\"{table}\"");
        command.Parameters.AddWithValue("count", columnCount);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public static async Task<long> CountAsync(string connectionString, string table, string? where = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT count(*) FROM \"{table}\"" + (where is null ? "" : $" WHERE {where}");
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public static async Task<IReadOnlyList<string>> AppliedMigrationsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return await QueryAsync(connection, "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"");
    }

    private static async Task<List<string>> QueryAsync(NpgsqlConnection connection, string sql)
    {
        var rows = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(reader.GetString(0));
        return rows;
    }
}
