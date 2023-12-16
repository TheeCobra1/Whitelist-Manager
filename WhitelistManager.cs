using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Whitelist Manager", "Cobra", "2.0.4")]
    [Description("Manage a dynamic whitelist for your Rust server.")]
    class WhitelistManager : CovalencePlugin
    {
        private HashSet<string> whitelist = new HashSet<string>();
        private const int ItemsPerPage = 10;
        private const string PermissionAdmin = "whitelistmanager.admin";

        void Init()
        {
            permission.RegisterPermission(PermissionAdmin, this);
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
            if (!HasPermissionOrNotify(player, PermissionAdmin))
                return;

            if (args.Length == 0)
            {
                player.Reply(GetMessage("Usage", player));
                return;
            }

            var action = args[0].ToLower();

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

            var targetPlayer = args[1].ToLower();

            if (whitelist.Contains(targetPlayer))
            {
                player.Reply(GetMessage("AlreadyWhitelisted", player).Replace("{player}", targetPlayer));
                return;
            }

            whitelist.Add(targetPlayer);
            SaveWhitelistData();
            player.Reply(GetMessage("AddedToWhitelist", player).Replace("{player}", targetPlayer));
        }

        void RemovePlayerFromWhitelist(IPlayer player, string[] args)
        {
            if (!HasValidArgumentsOrNotify(player, args, 2, GetMessage("UsageRemove", player)))
                return;

            var playerToRemove = args[1].ToLower();

            if (!whitelist.Contains(playerToRemove))
            {
                player.Reply(GetMessage("PlayerNotFound", player).Replace("{player}", playerToRemove));
                return;
            }

            whitelist.Remove(playerToRemove);
            SaveWhitelistData();
            player.Reply(GetMessage("RemovedFromWhitelist", player).Replace("{player}", playerToRemove));
        }

        void ListWhitelistedPlayers(IPlayer player, string[] args)
        {
            int page = args.Length > 1 && int.TryParse(args[1], out int pageNum) ? Math.Max(1, pageNum) : 1;
            int totalItems = whitelist.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)ItemsPerPage);
            page = Math.Min(page, totalPages);

            var paginatedList = whitelist.Skip((page - 1) * ItemsPerPage).Take(ItemsPerPage);
            var playerDetailsList = paginatedList.Select(pid => {
                var pl = covalence.Players.FindPlayerById(pid);
                return pl != null ? $"{pl.Name} ({pid})" : $"({pid})";
            });

            var message = GetMessage("ListWhitelisted", player).Replace("{players}", string.Join("\n", playerDetailsList));
            player.Reply(message);
        }

        void SearchWhitelistedPlayers(IPlayer player, string[] args)
        {
            if (!HasValidArgumentsOrNotify(player, args, 2, GetMessage("UsageSearch", player)))
                return;

            string searchTerm = args[1].ToLower();
            var foundPlayers = whitelist
                .Where(id => id.ToLower().Contains(searchTerm))
                .Select(pid => {
                    var pl = covalence.Players.FindPlayerById(pid);
                    return pl != null ? $"{pl.Name} ({pid})" : $"({pid})";
                })
                .ToList();

            if (foundPlayers.Any())
            {
                player.Reply(GetMessage("FoundPlayers", player).Replace("{players}", string.Join(", ", foundPlayers)));
            }
            else
            {
                player.Reply(GetMessage("NoPlayersFound", player).Replace("{searchTerm}", searchTerm));
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
            Interface.Oxide.DataFileSystem.WriteObject("WhitelistManager", whitelist);
        }

        void LoadWhitelistData()
        {
            var whitelistData = Interface.Oxide.DataFileSystem.ReadObject<HashSet<string>>("WhitelistManager");
            if (whitelistData != null)
            {
                whitelist = new HashSet<string>(whitelistData);
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
                ["ListWhitelisted"] = "Whitelisted Players: {players}",
                ["Usage"] = "<color=yellow>Usage:</color> /whitelist <add|remove|list|search>",
                ["UsageAdd"] = "<color=yellow>Usage:</color> /whitelist add <player>",
                ["UsageRemove"] = "<color=yellow>Usage:</color> /whitelist remove <player>",
                ["UsageSearch"] = "<color=yellow>Usage:</color> /whitelist search <player>",
                ["FoundPlayers"] = "<color=green>Found players:</color> {players}",
                ["NoPlayersFound"] = "<color=yellow>No players found matching:</color> {searchTerm}"
            }, this);
        }
    }
}