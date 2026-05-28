using Microsoft.Data.Sqlite;
using Serilog;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Solace.DB;
using Solace.DB.Models.Admin;
using Solace.DB.Models.Player;

namespace Solace.LauncherUI.Utils;

internal static class DataUtils
{
    public static async IAsyncEnumerable<(string Id, ShopTab Tab)> GetShopTabsAsync(EarthDB db, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var connection = db.OpenConnection(false);

        using (var command = new SqliteCommand($"""
            SELECT id, value FROM {EarthDB.ObjectsTable} WHERE type = 'shop_tab' ORDER BY json_extract(value, '$.order');
            """, connection))
        {
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    string id = reader.GetString(0);
                    var tab = EarthDB.FromJson<ShopTab>(reader.GetString(1));
                    Debug.Assert(tab is not null);
                    yield return (id, tab);
                }
            }
        }
    }

    public static async Task UpdateShopTabAsync(EarthDB db, string id, ShopTab tab, CancellationToken cancellationToken = default)
    {
        await new EarthDB.Query(true)
            .Update("shop_tab", id, tab)
            .ExecuteAsync(db, cancellationToken);
    }

    public static async Task DeleteShopTabAsync(EarthDB db, string id, CancellationToken cancellationToken = default)
    {
        await db.ExecuteCommandAsync(true, async command =>
        {
            command.CommandText = $"DELETE FROM {EarthDB.ObjectsTable} WHERE type = 'shop_tab' AND id = @id";
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public static async IAsyncEnumerable<(string Id, ShopOffer Offer)> GetShopOffersAsync(EarthDB db, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var connection = db.OpenConnection(false);

        using (var command = new SqliteCommand($"""
            SELECT id, value FROM {EarthDB.ObjectsTable} WHERE type = 'shop_offer';
            """, connection))
        {
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    string id = reader.GetString(0);
                    var offer = EarthDB.FromJson<ShopOffer>(reader.GetString(1));
                    Debug.Assert(offer is not null);
                    yield return (id, offer);
                }
            }
        }
    }

    public static async Task UpdateShopOfferAsync(EarthDB db, string id, ShopOffer offer, CancellationToken cancellationToken = default)
    {
        await new EarthDB.Query(true)
            .Update("shop_offer", id, offer)
            .ExecuteAsync(db, cancellationToken);
    }

    public static async Task DeleteShopOfferAsync(EarthDB db, string id, CancellationToken cancellationToken = default)
    {
        await db.ExecuteCommandAsync(true, async command =>
        {
            command.CommandText = $"DELETE FROM {EarthDB.ObjectsTable} WHERE type = 'shop_offer' AND id = @id";
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }
    public static SqliteConnection? OpenLiveDB(Settings settings)
    {
        try
        {
            var connection = new SqliteConnection("Data Source=" + settings.LiveDatabaseConnectionString);
            connection.Open();
            return connection;
        }
        catch
        {
            return null;
        }
    }

    public static long? GetPlayerCount(EarthDB db)
    {
        long? playerCount = null;
        try
        {
            db.ExecuteCommand(false, command =>
            {
                command.CommandText = $"""
                    SELECT COUNT(DISTINCT id) FROM {EarthDB.ObjectsTable};
                    """;

                playerCount = command.ExecuteScalar() as long?;
            });
        }
        catch
        {
        }

        return playerCount;
    }

    public static async Task<long?> GetPlayerCountAsync(EarthDB db, CancellationToken cancellationToken = default)
    {
        long? playerCount = null;
        try
        {
            await db.ExecuteCommandAsync(false, command =>
            {
                command.CommandText = $"""
                    SELECT COUNT(DISTINCT id) FROM {EarthDB.ObjectsTable};
                    """;

                playerCount = command.ExecuteScalar() as long?;
            }, cancellationToken);
        }
        catch
        {
        }

        return playerCount;
    }

    public static async IAsyncEnumerable<string> GetPlayersAsync(EarthDB db, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var connection = db.OpenConnection(false);

        using (var command = new SqliteCommand($"""
            SELECT DISTINCT id FROM {EarthDB.ObjectsTable};
            """, connection))
        {
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (reader.Read())
                {
                    yield return reader.GetString(0);
                }
            }
        }
    }

    public static async IAsyncEnumerable<(string Id, Profile Profile)> GetAllProfilesAsync(EarthDB db, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var connection = db.OpenConnection(false);

        HashSet<string> returnedPlayers = [];

        using (var command = new SqliteCommand($"""
            SELECT id, value FROM {EarthDB.ObjectsTable} WHERE type = 'profile';
            """, connection))
        {
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    string id = reader.GetString(0);
                    var profile = EarthDB.FromJson<Profile>(reader.GetString(1));
                    Debug.Assert(profile is not null);

                    returnedPlayers.Add(id);
                    yield return (id, profile);
                }
            }
        }

        await foreach (string playerId in GetPlayersAsync(db, cancellationToken))
        {
            if (!returnedPlayers.Contains(playerId))
            {
                yield return (playerId, new Profile());
            }
        }
    }

    public static async Task<string?> GetUsername(string userId, SqliteConnection liveConnection, CancellationToken cancellationToken = default)
    {
        try
        {
            using (var command = new SqliteCommand($"""
                SELECT Username FROM Accounts WHERE Id = @id;
                """, liveConnection))
            {
                command.Parameters.AddWithValue("@id", userId);

                return await command.ExecuteScalarAsync(cancellationToken) as string;
            }
        }
        catch
        {
            return null;
        }
    }

    public static IAsyncEnumerable<(string Id, string? Username, Profile Profile)> GetFullProfilesAsync(EarthDB db, SqliteConnection? liveConnection, CancellationToken cancellationToken = default)
    {
        if (liveConnection is null)
        {
            return GetAllProfilesAsync(db, cancellationToken)
                .Select(item => (item.Id, (string?)null, item.Profile));
        }

        return GetAllProfilesAsync(db, cancellationToken)
            .Select(async ((string Id, Profile Profile) item, CancellationToken cancellationToken) => (item.Id, await GetUsername(item.Id, liveConnection, cancellationToken), item.Profile));
    }

    public static async Task UpdateUsernameAsync(string userId, string? newUsername, SqliteConnection liveConnection, CancellationToken cancellationToken = default)
    {
        try
        {
            using var command = new SqliteCommand("""
                UPDATE Accounts SET Username = @username WHERE Id = @id;
                """, liveConnection);

            command.Parameters.AddWithValue("@username", string.IsNullOrWhiteSpace(newUsername) ? DBNull.Value : newUsername);
            command.Parameters.AddWithValue("@id", userId);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error($"Failed to update username for {userId}: {ex}");
        }
    }

    public static async Task UpdateProfileAsync(EarthDB db, string userId, Profile profile, CancellationToken cancellationToken = default)
    {
        try
        {
            var updateQuery = new EarthDB.Query(true)
                .Update("profile", userId, profile)
                .ExecuteAsync(db, cancellationToken);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error($"Failed to update profile for {userId}: {ex}");
        }
    }

    public static async Task DeletePlayerAsync(EarthDB earthDB, SqliteConnection? liveConnection, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (liveConnection is not null)
            {
                using (var liveCommand = new SqliteCommand("""
                    DELETE FROM Accounts WHERE Id = @id;
                    """, liveConnection))
                {
                    liveCommand.Parameters.AddWithValue("@id", userId);
                    await liveCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await earthDB.ExecuteCommandAsync(true, async command =>
            {
                command.CommandText = $"""
                DELETE FROM {EarthDB.ObjectsTable} WHERE id = @id;
                """;
                command.Parameters.AddWithValue("@id", userId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }, cancellationToken);

            Log.Information("Successfully deleted player {UserId}.", userId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete player {UserId}", userId);
            throw;
        }
    }

    public static unsafe string DataToUri(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return "data:application/octet-stream;base64,";
        }

        const string Prefix = "data:application/octet-stream;base64,";

        int base64Length = ((data.Length + 2) / 3) * 4;
        int totalLength = Prefix.Length + base64Length;

        fixed (byte* ptr = data)
        {
            var state = ((IntPtr)ptr, data.Length);

            return string.Create(totalLength, state, static (span, s) =>
            {
                Prefix.AsSpan().CopyTo(span);

                var byteSpan = new ReadOnlySpan<byte>((void*)s.Item1, s.Item2);

                Convert.TryToBase64Chars(byteSpan, span[Prefix.Length..], out _);
            });
        }
    }
}
