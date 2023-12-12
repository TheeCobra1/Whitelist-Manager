using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Whitelist Manager", "Cobra", "2.0.2")]
    [Description("Manage a dynamic whitelist for your Rust server.")]
    class WhitelistManager : CovalencePlugin
    {
        private HashSet<string> whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private const int ItemsPerPage = 10;

        void Init()
        {
            permission.RegisterPermission("whitelistmanager.admin", this);
            permission.RegisterPermission("whitelistmanager.bypass", this);
            LoadWhitelistData();
        }

        bool IsWhitelisted(string playerId)
        {
            return whitelist.Contains(playerId);
        }

        void OnUserConnected(IPlayer player)
        {
            if (!IsWhitelisted(player.Id))
            {
                player.Kick(GetMessage("NotWhitelisted", player));
            }
        }

        [Command("whitelist")]
        void CmdWhitelist(IPlayer player, string command, string[] args)
        {
            if (!HasPermissionOrNotify(player, "whitelistmanager.admin"))
                return;

            if (args.Length == 0)
            {
                player.Reply(GetMessage("Usage", player));
                return;
            }

            var action = args[0].ToLower(CultureInfo.InvariantCulture);

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
                default:
                    player.Reply(GetMessage("Usage", player));
                    break;
            }
        }

        void AddPlayerToWhitelist(IPlayer player, string[] args)
        {
            if (!HasValidArgumentsOrNotify(player, args, 2, GetMessage("UsageAdd", player)))
                return;

            var targetPlayer = args[1].ToLower(CultureInfo.InvariantCulture);

            if (whitelist.Contains(targetPlayer))
            {
                player.Reply(GetMessage("AlreadyWhitelisted", player).Replace("{player}", targetPlayer, StringComparison.Ordinal));
                return;
            }

            whitelist.Add(targetPlayer);
            SaveWhitelistData();
            player.Reply(GetMessage("AddedToWhitelist", player).Replace("{player}", targetPlayer, StringComparison.Ordinal));
        }

        void RemovePlayerFromWhitelist(IPlayer player, string[] args)
        {
            if (!HasValidArgumentsOrNotify(player, args, 2, GetMessage("UsageRemove", player)))
                return;

            var playerToRemove = args[1].ToLower(CultureInfo.InvariantCulture);

            if (!whitelist.Contains(playerToRemove))
            {
                player.Reply(GetMessage("PlayerNotFound", player).Replace("{player}", playerToRemove, StringComparison.Ordinal));
                return;
            }

            whitelist.Remove(playerToRemove);
            SaveWhitelistData();
            player.Reply(GetMessage("RemovedFromWhitelist", player).Replace("{player}", playerToRemove, StringComparison.Ordinal));
        }

        void ListWhitelistedPlayers(IPlayer player, string[] args)
        {
            int page = args.Length > 1 && int.TryParse(args[1], out int pageNum) ? Math.Max(1, pageNum) : 1;
            int totalItems = whitelist.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)ItemsPerPage);
            page = Math.Min(page, totalPages);

            var paginatedList = whitelist.Skip((page - 1) * ItemsPerPage).Take(ItemsPerPage);
            var message = $"Whitelisted Players (Page {page} of {totalPages}):\n" + string.Join("\n", paginatedList);

            player.Reply(message);
        }

        void SearchWhitelistedPlayers(IPlayer player, string[] args)
        {
            if (!HasValidArgumentsOrNotify(player, args, 2, GetMessage("UsageSearch", player)))
                return;

            string searchTerm = args[1].ToLower(CultureInfo.InvariantCulture);
            var foundPlayers = whitelist.Where(id => id.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

            if (foundPlayers.Any())
            {
                player.Reply($"<color=green>Found players:</color> {string.Join(", ", foundPlayers)}");
            }
            else
            {
                player.Reply($"<color=yellow>No players found matching:</color> {searchTerm}");
            }
        }

        bool HasPermissionOrNotify(IPlayer player, string permissionName)
        {
            if (player.HasPermission(permissionName))
                return true;

            player.Reply(GetMessage("NoPermission", player));
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
            Interface.Oxide.DataFileSystem.WriteObject("WhitelistManager", whitelist.ToList());
        }

        void LoadWhitelistData()
        {
            var whitelistData = Interface.Oxide.DataFileSystem.ReadObject<List<string>>("WhitelistManager");
            if (whitelistData != null)
            {
                whitelist = new HashSet<string>(whitelistData, StringComparer.OrdinalIgnoreCase);
            }
        }

        private string GetMessage(string key, IPlayer player = null) =>
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
                ["PlayerNotFound"] = "<color=red>Error:</color> Player not found on the whitelist.",
                ["ListWhitelisted"] = "Whitelisted players: {players}",
                ["Usage"] = "<color=yellow>Usage:</color> /whitelist <add|remove|list|search>",
                ["UsageAdd"] = "<color=yellow>Usage:</color> /whitelist add <player>",
                ["UsageRemove"] = "<color=yellow>Usage:</color> /whitelist remove <player>",
                ["UsageSearch"] = "<color=yellow>Usage:</color> /whitelist search <player>"
            }, this);
        }
    }
}