using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Whitelist Manager", "Cobra", "2.0.0")]
    [Description("Manage a dynamic whitelist for your Rust server.")]
    class WhitelistManager : CovalencePlugin
    {
        private HashSet<string> whitelist = new HashSet<string>();

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
            if (args.Length == 0)
            {
                player.Reply("Usage: /whitelist <add|remove|list> <player>");
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
                    ListWhitelistedPlayers(player);
                    break;

                default:
                    player.Reply("Usage: /whitelist <add|remove|list> <player>");
                    break;
            }
        }

        void AddPlayerToWhitelist(IPlayer player, string[] args)
        {
            if (!HasPermissionOrNotify(player, "whitelistmanager.admin"))
                return;

            if (!HasValidArgumentsOrNotify(player, args, 2, "Usage: /whitelist add <player>"))
                return;

            var targetPlayer = args[1];

            if (whitelist.Contains(targetPlayer))
            {
                player.Reply(GetMessage("AlreadyWhitelisted", player).Replace("{player}", targetPlayer));
                return;
            }

            whitelist.Add(targetPlayer);
            SaveWhitelistData();
            player.Reply(GetMessage("AddedToWhitelist", player).Replace("{player}", targetPlayer));

            // Grant whitelistmanager.bypass permission to the whitelisted player
            permission.GrantUserPermission(targetPlayer, "whitelistmanager.bypass", this);
        }

        void RemovePlayerFromWhitelist(IPlayer player, string[] args)
        {
            if (!HasPermissionOrNotify(player, "whitelistmanager.admin"))
                return;

            if (!HasValidArgumentsOrNotify(player, args, 2, "Usage: /whitelist remove <player>"))
                return;

            var playerToRemove = args[1];

            if (!whitelist.Contains(playerToRemove))
            {
                player.Reply(GetMessage("PlayerNotFound", player));
                return;
            }

            whitelist.Remove(playerToRemove);
            SaveWhitelistData();
            player.Reply(GetMessage("RemovedFromWhitelist", player).Replace("{player}", playerToRemove));

            // Revoke whitelistmanager.bypass permission from the unwhitelisted player
            permission.RevokeUserPermission(playerToRemove, "whitelistmanager.bypass");
        }

        void ListWhitelistedPlayers(IPlayer player)
        {
            if (!HasPermissionOrNotify(player, "whitelistmanager.admin"))
                return;

            var whitelistedPlayers = string.Join(", ", whitelist);
            player.Reply(GetMessage("ListWhitelisted", player).Replace("{players}", whitelistedPlayers));
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
            if (args.Length != expectedArgsCount)
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
                whitelist = new HashSet<string>(whitelistData);
            }
        }

        #region Localization

        private string GetMessage(string key, IPlayer player = null) =>
            lang.GetMessage(key, this, player?.Id);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotWhitelisted"] = "You are not whitelisted on this server.",
                ["AlreadyWhitelisted"] = "{player} is already whitelisted.",
                ["AddedToWhitelist"] = "{player} has been added to the whitelist.",
                ["RemovedFromWhitelist"] = "{player} has been removed from the whitelist.",
                ["NoPermission"] = "You do not have permission to use this command.",
                ["PlayerNotFound"] = "Player not found on the whitelist.",
                ["ListWhitelisted"] = "Whitelisted players: {players}"
            }, this);
        }

        #endregion
    }
}