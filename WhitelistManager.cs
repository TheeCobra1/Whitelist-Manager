using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Whitelist Manager", "Cobra", "2.5.0")]
    [Description("Advanced whitelist management with validation, logging, bulk operations, temporary entries, configuration, and performance optimizations.")]
    class WhitelistManager : CovalencePlugin
    {
        private class WhitelistEntry
        {
            public string PlayerId { get; set; }
            public DateTime? ExpirationDate { get; set; }
            public string AddedBy { get; set; }
            public DateTime AddedDate { get; set; }
        }

        private class Configuration
        {
            public int ItemsPerPage { get; set; } = 10;
            public bool EnableLogging { get; set; } = true;
            public int MaxLogEntries { get; set; } = 1000;
            public float CleanupInterval { get; set; } = 300f;
            public bool KickOnExpiration { get; set; } = true;
            public string KickMessage { get; set; } = "Your whitelist access has expired.";
            public bool NotifyAdminsOnExpiration { get; set; } = true;
            public bool EnableDiscordWebhook { get; set; } = false;
            public string DiscordWebhookUrl { get; set; } = "";
            public Dictionary<string, string> CustomMessages { get; set; } = new Dictionary<string, string>();
            public bool UseAsyncOperations { get; set; } = true;
            public int BatchSize { get; set; } = 100;
        }

        private Configuration config;
        private Dictionary<string, WhitelistEntry> whitelistData = new Dictionary<string, WhitelistEntry>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object whitelistLock = new object();
        private bool isDirty = false;
        private Timer autoSaveTimer;
        private const string PermissionAdmin = "whitelistmanager.admin";
        private const string DataFileName = "WhitelistManager";
        private const string LogFileName = "WhitelistManager_Log";
        private readonly System.Text.RegularExpressions.Regex steamIdRegex = new System.Text.RegularExpressions.Regex(@"^7656119\d{10}$");

        void Init()
        {
            permission.RegisterPermission(PermissionAdmin, this);
            LoadConfig();
            TryLoadWhitelistData();
            timer.Every(config.CleanupInterval, CleanupExpiredEntries);
            autoSaveTimer = timer.Every(60f, () =>
            {
                if (isDirty)
                {
                    SaveWhitelistData();
                    isDirty = false;
                }
            });
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    PrintWarning("Creating new configuration file");
                    config = new Configuration();
                }
            }
            catch
            {
                PrintWarning("Failed to load config, using default values");
                config = new Configuration();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            SaveConfig();
        }

        bool IsPlayerWhitelisted(string playerId)
        {
            lock (whitelistLock)
            {
                if (!whitelistData.TryGetValue(playerId, out var entry))
                    return false;

                if (entry.ExpirationDate.HasValue && entry.ExpirationDate.Value < DateTime.Now)
                {
                    whitelistData.Remove(playerId);
                    whitelist.Remove(playerId);
                    isDirty = true;
                    return false;
                }

                return true;
            }
        }

        void OnUserConnected(IPlayer player)
        {
            if (!IsPlayerWhitelisted(player.Id))
            {
                timer.Once(0.1f, () => player.Kick(GetLocalizedMessage("NotWhitelisted", player)));
            }
        }

        void Unload()
        {
            if (isDirty)
            {
                SaveWhitelistData();
            }
            autoSaveTimer?.Destroy();
        }

        [Command("whitelist")]
        void CmdWhitelist(IPlayer player, string command, string[] args)
        {
            if (!HasPermissionOrNotify(player, PermissionAdmin))
                return;

            if (args.Length == 0)
            {
                player.Reply(GetLocalizedMessage("Usage", player));
                return;
            }

            var action = args[0].ToLowerInvariant();

            switch (action)
            {
                case "add":
                    AddPlayerToWhitelist(player, args);
                    break;
                case "remove":
                    RemovePlayerFromWhitelist(player, args);
                    break;
                case "list":
                    ListWhitelistedPlayers(player, args);
                    break;
                case "search":
                    SearchWhitelistedPlayers(player, args);
                    break;
                case "clear":
                    ClearWhitelist(player);
                    break;
                case "count":
                    ShowWhitelistCount(player);
                    break;
                case "reload":
                    ReloadWhitelist(player);
                    break;
                case "config":
                    ReloadConfig(player);
                    break;
                case "bulk":
                    ProcessBulkOperation(player, args);
                    break;
                case "export":
                    ExportWhitelist(player);
                    break;
                case "import":
                    ImportWhitelist(player, args);
                    break;
                case "temp":
                    AddTemporaryWhitelist(player, args);
                    break;
                case "info":
                    ShowPlayerInfo(player, args);
                    break;
                default:
                    player.Reply(GetLocalizedMessage("Usage", player));
                    break;
            }
        }

        void AddPlayerToWhitelist(IPlayer player, string[] args)
        {
            if (!HasValidArgumentsOrNotify(player, args, 2, GetLocalizedMessage("UsageAdd", player)))
                return;

            var targetPlayer = args[1];

            if (!IsValidPlayerId(targetPlayer))
            {
                player.Reply(GetLocalizedMessage("InvalidPlayerId", player).Replace("{player}", targetPlayer));
                return;
            }

            if (whitelistData.ContainsKey(targetPlayer))
            {
                player.Reply(GetLocalizedMessage("AlreadyWhitelisted", player).Replace("{player}", targetPlayer));
                return;
            }

            lock (whitelistLock)
            {
                whitelistData[targetPlayer] = new WhitelistEntry
                {
                    PlayerId = targetPlayer,
                    ExpirationDate = null,
                    AddedBy = player.Name + " (" + player.Id + ")",
                    AddedDate = DateTime.Now
                };
                whitelist.Add(targetPlayer);
                isDirty = true;
            }
            LogWhitelistAction(player, "ADD", targetPlayer);
            player.Reply(GetLocalizedMessage("AddedToWhitelist", player).Replace("{player}", targetPlayer));
        }

        void RemovePlayerFromWhitelist(IPlayer player, string[] args)
        {
            if (!HasValidArgumentsOrNotify(player, args, 2, GetLocalizedMessage("UsageRemove", player)))
                return;

            var playerToRemove = args[1];

            if (!IsValidPlayerId(playerToRemove))
            {
                player.Reply(GetLocalizedMessage("InvalidPlayerId", player).Replace("{player}", playerToRemove));
                return;
            }

            if (!whitelistData.ContainsKey(playerToRemove))
            {
                player.Reply(GetLocalizedMessage("PlayerNotFound", player).Replace("{player}", playerToRemove));
                return;
            }

            lock (whitelistLock)
            {
                whitelistData.Remove(playerToRemove);
                whitelist.Remove(playerToRemove);
                isDirty = true;
            }
            LogWhitelistAction(player, "REMOVE", playerToRemove);
            player.Reply(GetLocalizedMessage("RemovedFromWhitelist", player).Replace("{player}", playerToRemove));
        }

        void ListWhitelistedPlayers(IPlayer player, string[] args)
        {
            if (whitelist.Count == 0)
            {
                player.Reply(GetLocalizedMessage("EmptyWhitelist", player));
                return;
            }

            int page = args.Length > 1 && int.TryParse(args[1], out int pageNum) ? Math.Max(1, pageNum) : 1;
            int totalItems = whitelist.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)config.ItemsPerPage);
            page = Math.Min(page, totalPages);

            var paginatedList = whitelistData.Values
                .OrderBy(x => x.PlayerId)
                .Skip((page - 1) * config.ItemsPerPage)
                .Take(config.ItemsPerPage);

            var playerDetailsList = paginatedList.Select(entry =>
            {
                var pl = covalence.Players.FindPlayerById(entry.PlayerId);
                var name = pl != null ? pl.Name : "Unknown";
                var expiry = entry.ExpirationDate.HasValue ? $" [Expires: {entry.ExpirationDate.Value:yyyy-MM-dd HH:mm}]" : "";
                return $"{name} ({entry.PlayerId}){expiry}";
            });

            var header = GetLocalizedMessage("ListHeader", player)
                .Replace("{page}", page.ToString())
                .Replace("{totalPages}", totalPages.ToString())
                .Replace("{count}", totalItems.ToString());

            var message = $"{header}\n{string.Join("\n", playerDetailsList)}";
            player.Reply(message);
        }

        void SearchWhitelistedPlayers(IPlayer player, string[] args)
        {
            if (!HasValidArgumentsOrNotify(player, args, 2, GetLocalizedMessage("UsageSearch", player)))
                return;

            string searchTerm = args[1].ToLowerInvariant();
            var foundPlayers = whitelist
                .Where(id => id.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(x => x)
                .Select(pid =>
                {
                    var pl = covalence.Players.FindPlayerById(pid);
                    return pl != null ? $"{pl.Name} ({pid})" : $"Unknown ({pid})";
                })
                .ToList();

            if (foundPlayers.Any())
            {
                player.Reply(GetLocalizedMessage("FoundPlayers", player)
                    .Replace("{count}", foundPlayers.Count.ToString())
                    .Replace("{players}", string.Join("\n", foundPlayers)));
            }
            else
            {
                player.Reply(GetLocalizedMessage("NoPlayersFound", player)
                    .Replace("{searchTerm}", searchTerm));
            }
        }

        void ClearWhitelist(IPlayer player)
        {
            if (whitelist.Count == 0)
            {
                player.Reply(GetLocalizedMessage("EmptyWhitelist", player));
                return;
            }

            int count;
            lock (whitelistLock)
            {
                count = whitelistData.Count;
                whitelistData.Clear();
                whitelist.Clear();
                isDirty = true;
            }
            LogWhitelistAction(player, "CLEAR", $"Cleared {count} players");
            player.Reply(GetLocalizedMessage("WhitelistCleared", player).Replace("{count}", count.ToString()));
        }

        void ShowWhitelistCount(IPlayer player)
        {
            player.Reply(GetLocalizedMessage("WhitelistCount", player).Replace("{count}", whitelist.Count.ToString()));
        }

        void ReloadWhitelist(IPlayer player)
        {
            TryLoadWhitelistData();
            player.Reply(GetLocalizedMessage("WhitelistReloaded", player));
        }

        void ReloadConfig(IPlayer player)
        {
            LoadConfig();
            player.Reply(GetLocalizedMessage("ConfigReloaded", player));
        }

        bool HasPermissionOrNotify(IPlayer player, string permissionName)
        {
            if (player.HasPermission(permissionName))
                return true;

            player.Reply(GetLocalizedMessage("NoPermission", player));
            return false;
        }

        bool HasValidArgumentsOrNotify(IPlayer player, string[] args, int expectedArgsCount, string message)
        {
            if (args.Length < expectedArgsCount)
            {
                player.Reply(message);
                return false;
            }
            return true;
        }

        bool IsValidPlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return false;

            if (playerId.Length == 17 && playerId.All(char.IsDigit))
                return steamIdRegex.IsMatch(playerId);

            return false;
        }

        void LogWhitelistAction(IPlayer player, string action, string details)
        {
            if (!config.EnableLogging)
                return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = $"[{timestamp}] {action} by {player.Name} ({player.Id}): {details}";
            
            try
            {
                var logData = Interface.Oxide.DataFileSystem.ReadObject<List<string>>(LogFileName) ?? new List<string>();
                logData.Add(logEntry);
                
                if (logData.Count > config.MaxLogEntries)
                    logData.RemoveRange(0, logData.Count - config.MaxLogEntries);
                
                Interface.Oxide.DataFileSystem.WriteObject(LogFileName, logData);
            }
            catch (Exception ex)
            {
                PrintWarning($"Error logging whitelist action: {ex.Message}");
            }
        }

        void ProcessBulkOperation(IPlayer player, string[] args)
        {
            if (args.Length < 3)
            {
                player.Reply(GetLocalizedMessage("UsageBulk", player));
                return;
            }

            var operation = args[1].ToLowerInvariant();
            var playerIds = args.Skip(2).ToArray();
            var successCount = 0;
            var failCount = 0;

            foreach (var playerId in playerIds)
            {
                if (!IsValidPlayerId(playerId))
                {
                    failCount++;
                    continue;
                }

                if (operation == "add")
                {
                    if (!whitelistData.ContainsKey(playerId))
                    {
                        lock (whitelistLock)
                        {
                            whitelistData[playerId] = new WhitelistEntry
                            {
                                PlayerId = playerId,
                                ExpirationDate = null,
                                AddedBy = player.Name + " (" + player.Id + ")",
                                AddedDate = DateTime.Now
                            };
                            whitelist.Add(playerId);
                        }
                        successCount++;
                    }
                    else
                        failCount++;
                }
                else if (operation == "remove")
                {
                    if (whitelistData.ContainsKey(playerId))
                    {
                        lock (whitelistLock)
                        {
                            whitelistData.Remove(playerId);
                            whitelist.Remove(playerId);
                        }
                        successCount++;
                    }
                    else
                        failCount++;
                }
            }

            if (successCount > 0)
            {
                isDirty = true;
                LogWhitelistAction(player, $"BULK_{operation.ToUpper()}", $"Success: {successCount}, Failed: {failCount}");
            }

            player.Reply(GetLocalizedMessage("BulkOperationComplete", player)
                .Replace("{operation}", operation)
                .Replace("{success}", successCount.ToString())
                .Replace("{failed}", failCount.ToString()));
        }

        void ExportWhitelist(IPlayer player)
        {
            if (whitelist.Count == 0)
            {
                player.Reply(GetLocalizedMessage("EmptyWhitelist", player));
                return;
            }

            var exportData = string.Join("\n", whitelist.OrderBy(x => x));
            var fileName = $"WhitelistExport_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject(fileName, exportData);
                LogWhitelistAction(player, "EXPORT", fileName);
                player.Reply(GetLocalizedMessage("ExportSuccess", player).Replace("{filename}", fileName));
            }
            catch (Exception ex)
            {
                player.Reply(GetLocalizedMessage("ExportError", player));
                PrintWarning($"Error exporting whitelist: {ex.Message}");
            }
        }

        void ImportWhitelist(IPlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                player.Reply(GetLocalizedMessage("UsageImport", player));
                return;
            }

            var fileName = args[1];
            
            try
            {
                var importData = Interface.Oxide.DataFileSystem.ReadObject<string>(fileName);
                if (string.IsNullOrEmpty(importData))
                {
                    player.Reply(GetLocalizedMessage("ImportFileEmpty", player));
                    return;
                }

                var lines = importData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var importCount = 0;
                var invalidCount = 0;

                foreach (var line in lines)
                {
                    var playerId = line.Trim();
                    if (IsValidPlayerId(playerId) && !whitelistData.ContainsKey(playerId))
                    {
                        whitelistData[playerId] = new WhitelistEntry
                        {
                            PlayerId = playerId,
                            ExpirationDate = null,
                            AddedBy = player.Name + " (" + player.Id + ")",
                            AddedDate = DateTime.Now
                        };
                        whitelist.Add(playerId);
                        importCount++;
                    }
                    else if (!IsValidPlayerId(playerId))
                    {
                        invalidCount++;
                    }
                }

                if (importCount > 0)
                {
                    isDirty = true;
                    LogWhitelistAction(player, "IMPORT", $"Imported {importCount} players from {fileName}");
                }

                player.Reply(GetLocalizedMessage("ImportSuccess", player)
                    .Replace("{imported}", importCount.ToString())
                    .Replace("{invalid}", invalidCount.ToString()));
            }
            catch (Exception ex)
            {
                player.Reply(GetLocalizedMessage("ImportError", player));
                PrintWarning($"Error importing whitelist: {ex.Message}");
            }
        }

        void AddTemporaryWhitelist(IPlayer player, string[] args)
        {
            if (args.Length < 3)
            {
                player.Reply(GetLocalizedMessage("UsageTemp", player));
                return;
            }

            var targetPlayer = args[1];
            var duration = args[2];

            if (!IsValidPlayerId(targetPlayer))
            {
                player.Reply(GetLocalizedMessage("InvalidPlayerId", player).Replace("{player}", targetPlayer));
                return;
            }

            DateTime expirationDate;
            if (duration.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(duration.TrimEnd('h', 'H'), out int hours))
                    expirationDate = DateTime.Now.AddHours(hours);
                else
                {
                    player.Reply(GetLocalizedMessage("InvalidDuration", player));
                    return;
                }
            }
            else if (duration.EndsWith("d", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(duration.TrimEnd('d', 'D'), out int days))
                    expirationDate = DateTime.Now.AddDays(days);
                else
                {
                    player.Reply(GetLocalizedMessage("InvalidDuration", player));
                    return;
                }
            }
            else if (duration.EndsWith("m", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(duration.TrimEnd('m', 'M'), out int minutes))
                    expirationDate = DateTime.Now.AddMinutes(minutes);
                else
                {
                    player.Reply(GetLocalizedMessage("InvalidDuration", player));
                    return;
                }
            }
            else
            {
                player.Reply(GetLocalizedMessage("InvalidDuration", player));
                return;
            }

            lock (whitelistLock)
            {
                whitelistData[targetPlayer] = new WhitelistEntry
                {
                    PlayerId = targetPlayer,
                    ExpirationDate = expirationDate,
                    AddedBy = player.Name + " (" + player.Id + ")",
                    AddedDate = DateTime.Now
                };
                whitelist.Add(targetPlayer);
                isDirty = true;
            }
            LogWhitelistAction(player, "TEMP_ADD", $"{targetPlayer} until {expirationDate:yyyy-MM-dd HH:mm}");
            player.Reply(GetLocalizedMessage("TempWhitelistAdded", player)
                .Replace("{player}", targetPlayer)
                .Replace("{expiry}", expirationDate.ToString("yyyy-MM-dd HH:mm")));
        }

        void ShowPlayerInfo(IPlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                player.Reply(GetLocalizedMessage("UsageInfo", player));
                return;
            }

            var targetPlayer = args[1];
            if (!whitelistData.ContainsKey(targetPlayer))
            {
                player.Reply(GetLocalizedMessage("PlayerNotFound", player).Replace("{player}", targetPlayer));
                return;
            }

            var entry = whitelistData[targetPlayer];
            var pl = covalence.Players.FindPlayerById(entry.PlayerId);
            var name = pl != null ? pl.Name : "Unknown";

            var info = GetLocalizedMessage("PlayerInfo", player)
                .Replace("{name}", name)
                .Replace("{id}", entry.PlayerId)
                .Replace("{addedBy}", entry.AddedBy)
                .Replace("{addedDate}", entry.AddedDate.ToString("yyyy-MM-dd HH:mm"));

            if (entry.ExpirationDate.HasValue)
            {
                info += "\n" + GetLocalizedMessage("PlayerInfoExpiry", player)
                    .Replace("{expiry}", entry.ExpirationDate.Value.ToString("yyyy-MM-dd HH:mm"));
            }

            player.Reply(info);
        }

        void CleanupExpiredEntries()
        {
            List<string> expired;
            lock (whitelistLock)
            {
                expired = whitelistData.Where(kvp => kvp.Value.ExpirationDate.HasValue && kvp.Value.ExpirationDate.Value < DateTime.Now)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }

            if (expired.Count > 0)
            {
                lock (whitelistLock)
                {
                    foreach (var playerId in expired)
                    {
                        whitelistData.Remove(playerId);
                        whitelist.Remove(playerId);
                    }
                    isDirty = true;
                }
                    
                    if (config.KickOnExpiration)
                    {
                        var player = covalence.Players.FindPlayerById(playerId);
                        if (player != null && player.IsConnected)
                        {
                            player.Kick(config.KickMessage);
                        }
                    }

                    if (config.NotifyAdminsOnExpiration)
                    {
                        foreach (var admin in covalence.Players.Connected.Where(p => p.HasPermission(PermissionAdmin)))
                        {
                            admin.Message(GetLocalizedMessage("ExpiredNotification", admin).Replace("{player}", playerId));
                        }
                    }
                }
                Puts($"Cleaned up {expired.Count} expired whitelist entries.");
            }
        }

        void SaveWhitelistData()
        {
            try
            {
                Dictionary<string, WhitelistEntry> dataCopy;
                lock (whitelistLock)
                {
                    dataCopy = new Dictionary<string, WhitelistEntry>(whitelistData);
                }
                Interface.Oxide.DataFileSystem.WriteObject(DataFileName, dataCopy);
            }
            catch (IOException ex)
            {
                PrintWarning($"I/O Error saving whitelist data: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                PrintWarning($"Access Error saving whitelist data: {ex.Message}");
            }
            catch (Exception ex)
            {
                PrintWarning($"Unexpected Error saving whitelist data: {ex.Message}");
            }
        }

        void TryLoadWhitelistData()
        {
            try
            {
                var loadedData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, WhitelistEntry>>(DataFileName);
                if (loadedData != null && loadedData.Count > 0)
                {
                    whitelistData = new Dictionary<string, WhitelistEntry>(loadedData, StringComparer.OrdinalIgnoreCase);
                    whitelist = new HashSet<string>(whitelistData.Keys, StringComparer.OrdinalIgnoreCase);
                    CleanupExpiredEntries();
                    PrintWarning($"Loaded {whitelistData.Count} whitelisted players");
                }
                else
                {
                    var legacyData = Interface.Oxide.DataFileSystem.ReadObject<HashSet<string>>(DataFileName);
                    if (legacyData != null && legacyData.Count > 0)
                    {
                        foreach (var playerId in legacyData)
                        {
                            whitelistData[playerId] = new WhitelistEntry
                            {
                                PlayerId = playerId,
                                ExpirationDate = null,
                                AddedBy = "Legacy Import",
                                AddedDate = DateTime.Now
                            };
                        }
                        whitelist = new HashSet<string>(legacyData, StringComparer.OrdinalIgnoreCase);
                        SaveWhitelistData();
                        PrintWarning($"Migrated {whitelist.Count} legacy whitelist entries");
                    }
                }
            }
            catch (IOException ex)
            {
                PrintWarning($"I/O Error loading whitelist data: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                PrintWarning($"Access Error loading whitelist data: {ex.Message}");
            }
            catch (Exception ex)
            {
                PrintWarning($"Unexpected Error loading whitelist data: {ex.Message}");
            }
        }

        private string GetLocalizedMessage(string key, IPlayer player = null)
        {
            if (config?.CustomMessages != null && config.CustomMessages.ContainsKey(key))
                return config.CustomMessages[key];
            
            return lang.GetMessage(key, this, player?.Id);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotWhitelisted"] = "<color=red>Error:</color> You are not whitelisted on this server.",
                ["AlreadyWhitelisted"] = "<color=red>Error:</color> {player} is already whitelisted.",
                ["AddedToWhitelist"] = "<color=green>Success:</color> {player} has been added to the whitelist.",
                ["RemovedFromWhitelist"] = "<color=green>Success:</color> {player} has been removed from the whitelist.",
                ["NoPermission"] = "<color=yellow>Notice:</color> You do not have permission to use this command.",
                ["PlayerNotFound"] = "<color=red>Error:</color> Player {player} not found on the whitelist.",
                ["ListHeader"] = "<color=cyan>Whitelisted Players</color> (Page {page}/{totalPages}, Total: {count}):",
                ["Usage"] = "<color=yellow>Usage:</color> /whitelist <add|remove|list|search|clear|count|reload|bulk|export|import|temp|info|config>",
                ["UsageAdd"] = "<color=yellow>Usage:</color> /whitelist add <player>",
                ["UsageRemove"] = "<color=yellow>Usage:</color> /whitelist remove <player>",
                ["UsageSearch"] = "<color=yellow>Usage:</color> /whitelist search <term>",
                ["FoundPlayers"] = "<color=green>Found {count} players:</color>\n{players}",
                ["NoPlayersFound"] = "<color=yellow>No players found matching:</color> {searchTerm}",
                ["EmptyWhitelist"] = "<color=yellow>The whitelist is currently empty.</color>",
                ["WhitelistCleared"] = "<color=green>Success:</color> Cleared {count} players from the whitelist.",
                ["WhitelistCount"] = "<color=cyan>Total whitelisted players:</color> {count}",
                ["WhitelistReloaded"] = "<color=green>Success:</color> Whitelist has been reloaded from disk.",
                ["InvalidPlayerId"] = "<color=red>Error:</color> Invalid player ID format: {player}. Must be a valid Steam64 ID.",
                ["UsageBulk"] = "<color=yellow>Usage:</color> /whitelist bulk <add|remove> <player1> <player2> ...",
                ["BulkOperationComplete"] = "<color=green>Bulk {operation} complete:</color> {success} successful, {failed} failed.",
                ["ExportSuccess"] = "<color=green>Success:</color> Whitelist exported to {filename}",
                ["ExportError"] = "<color=red>Error:</color> Failed to export whitelist.",
                ["UsageImport"] = "<color=yellow>Usage:</color> /whitelist import <filename>",
                ["ImportSuccess"] = "<color=green>Success:</color> Imported {imported} players ({invalid} invalid entries skipped).",
                ["ImportError"] = "<color=red>Error:</color> Failed to import whitelist.",
                ["ImportFileEmpty"] = "<color=red>Error:</color> Import file is empty or not found.",
                ["UsageTemp"] = "<color=yellow>Usage:</color> /whitelist temp <player> <duration> (e.g., 1h, 7d, 30m)",
                ["InvalidDuration"] = "<color=red>Error:</color> Invalid duration format. Use: 1h (hours), 7d (days), or 30m (minutes).",
                ["TempWhitelistAdded"] = "<color=green>Success:</color> {player} temporarily whitelisted until {expiry}.",
                ["UsageInfo"] = "<color=yellow>Usage:</color> /whitelist info <player>",
                ["PlayerInfo"] = "<color=cyan>Player Info:</color>\nName: {name}\nID: {id}\nAdded by: {addedBy}\nAdded on: {addedDate}",
                ["PlayerInfoExpiry"] = "<color=yellow>Expires:</color> {expiry}",
                ["WhitelistExpired"] = "<color=red>Your whitelist access has expired.</color>",
                ["ExpiredNotification"] = "<color=yellow>Notice:</color> Player {player}'s whitelist access has expired.",
                ["ConfigReloaded"] = "<color=green>Success:</color> Configuration has been reloaded."
            }, this);
        }
    }
}