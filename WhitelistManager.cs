using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Whitelist Manager", "Cobra", "2.1.0")]
    [Description("Manage a dynamic whitelist for your Rust server.")]
    class WhitelistManager : CovalencePlugin
    {
        private HashSet<string> whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private const int ItemsPerPage = 10;
        private const string PermissionAdmin = "whitelistmanager.admin";
        private const string DataFileName = "WhitelistManager";

        void Init()
        {
            permission.RegisterPermission(PermissionAdmin, this);
            TryLoadWhitelistData();
        }

        bool IsPlayerWhitelisted(string playerId) => whitelist.Contains(playerId);

        void OnUserConnected(IPlayer player)
        {
            if (!IsPlayerWhitelisted(player.Id))
            {
                player.Kick(GetLocalizedMessage("NotWhitelisted", player));
            }
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
                default:
                    player.Reply(GetLocalizedMessage("Usage", player));
                    break;
            }
        }

        void AddPlayerToWhitelist(IPlayer player, string[] args)
        {
            if (!HasValidArgumentsOrNotify(player, args, 2, GetLocalizedMessage("UsageAdd", player)))
                return;

            var targetPlayer = args[1].ToLowerInvariant();

            if (whitelist.Contains(targetPlayer))
            {
                player.Reply(GetLocalizedMessage("AlreadyWhitelisted", player).Replace("{player}", targetPlayer));
                return;
            }

            whitelist.Add(targetPlayer);
            SaveWhitelistData();
            player.Reply(GetLocalizedMessage("AddedToWhitelist", player).Replace("{player}", targetPlayer));
        }

        void RemovePlayerFromWhitelist(IPlayer player, string[] args)
        {
            if (!HasValidArgumentsOrNotify(player, args, 2, GetLocalizedMessage("UsageRemove", player)))
                return;

            var playerToRemove = args[1].ToLowerInvariant();

            if (!whitelist.Remove(playerToRemove))
            {
                player.Reply(GetLocalizedMessage("PlayerNotFound", player).Replace("{player}", playerToRemove));
                return;
            }

            SaveWhitelistData();
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
            int totalPages = (int)Math.Ceiling(totalItems / (double)ItemsPerPage);
            page = Math.Min(page, totalPages);

            var paginatedList = whitelist
                .OrderBy(x => x)
                .Skip((page - 1) * ItemsPerPage)
                .Take(ItemsPerPage);

            var playerDetailsList = paginatedList.Select(pid =>
            {
                var pl = covalence.Players.FindPlayerById(pid);
                return pl != null ? $"{pl.Name} ({pid})" : $"Unknown ({pid})";
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

            int count = whitelist.Count;
            whitelist.Clear();
            SaveWhitelistData();
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

        void SaveWhitelistData()
        {
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject(DataFileName, whitelist);
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
                var loadedWhitelist = Interface.Oxide.DataFileSystem.ReadObject<HashSet<string>>(DataFileName);
                if (loadedWhitelist != null && loadedWhitelist.Count > 0)
                {
                    whitelist = new HashSet<string>(loadedWhitelist, StringComparer.OrdinalIgnoreCase);
                    PrintWarning($"Loaded {whitelist.Count} whitelisted players");
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

        private string GetLocalizedMessage(string key, IPlayer player = null) =>
            lang.GetMessage(key, this, player?.Id);

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
                ["Usage"] = "<color=yellow>Usage:</color> /whitelist <add|remove|list|search|clear|count|reload>",
                ["UsageAdd"] = "<color=yellow>Usage:</color> /whitelist add <player>",
                ["UsageRemove"] = "<color=yellow>Usage:</color> /whitelist remove <player>",
                ["UsageSearch"] = "<color=yellow>Usage:</color> /whitelist search <term>",
                ["FoundPlayers"] = "<color=green>Found {count} players:</color>\n{players}",
                ["NoPlayersFound"] = "<color=yellow>No players found matching:</color> {searchTerm}",
                ["EmptyWhitelist"] = "<color=yellow>The whitelist is currently empty.</color>",
                ["WhitelistCleared"] = "<color=green>Success:</color> Cleared {count} players from the whitelist.",
                ["WhitelistCount"] = "<color=cyan>Total whitelisted players:</color> {count}",
                ["WhitelistReloaded"] = "<color=green>Success:</color> Whitelist has been reloaded from disk."
            }, this);
        }
    }
}