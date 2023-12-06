using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Whitelist Manager", "Cobra", "1.8.0")]
    [Description("Manage a dynamic whitelist for your Rust server.")]
    class WhitelistManager : CovalencePlugin
    {
        private HashSet<string> whitelist = new HashSet<string>();
        private ConfigData configData;

        void Init()
        {
            permission.RegisterPermission("whitelistmanager.admin", this);
            permission.RegisterPermission("whitelistmanager.bypass", this);

            LoadDefaultMessages();
            LoadConfig();

            if (configData == null)
            {
                PrintError("Config file is not valid JSON or is missing. Creating a new one with default values.");
                configData = new ConfigData();
                SaveConfig();
            }

            LoadWhitelistData();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotWhitelisted"] = "You are not whitelisted on this server. Visit our website to apply for whitelist access.",
                ["AlreadyWhitelisted"] = "{player} is already whitelisted.",
                ["AddedToWhitelist"] = "{player} has been added to the whitelist.",
                ["RemovedFromWhitelist"] = "{player} has been removed from the whitelist.",
                ["AdminApproval"] = "You have been approved to join the whitelist.",
                ["NoPermission"] = "You do not have permission to use this command.",
                ["PlayerNotFound"] = "Player not found on the whitelist.",
                ["ListWhitelisted"] = "Whitelisted players: {players}"
            }, this);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch (Newtonsoft.Json.JsonException jsonException)
            {
                PrintWarning($"Failed to parse configuration file: {jsonException.Message}");
                LoadDefaultConfig();
            }
            catch (Exception ex)
            {
                PrintError($"An error occurred while loading the configuration: {ex.Message}");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData();
            SaveConfig();
        }

        void SaveConfig()
        {
            Config.WriteObject(configData);
        }

        void OnUserApprove(IPlayer player)
        {
            // Player approved for whitelist, add them to the whitelist and grant bypass permission
            whitelist.Add(player.Id);
            SaveWhitelistData();
            permission.GrantUserPermission(player.Id, "whitelistmanager.bypass", this);

            // Notify the player about the approval
            player.Reply(GetMessage("AdminApproval", player));
        }

        void OnUserConnected(IPlayer player)
        {
            if (!whitelist.Contains(player.Id))
            {
                // Player is not whitelisted, kick them and send the not whitelisted message
                player.Reply(GetMessage("NotWhitelisted", player));
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

            if (action == "add" && !player.HasPermission("whitelistmanager.admin"))
            {
                player.Reply(GetMessage("NoPermission", player));
                return;
            }

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

            var whitelistedPlayers = string.Join(", ", whitelist.ToArray());
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
            configData.WhitelistedPlayers = whitelist.ToList();
            SaveConfig();
        }

        void LoadWhitelistData()
        {
            var whitelistData = Config.ReadObject<ConfigData>();
            if (whitelistData != null)
            {
                whitelist = new HashSet<string>(whitelistData.WhitelistedPlayers);
            }
        }

        #region Configuration

        private class ConfigData
        {
            public List<string> WhitelistedPlayers { get; set; } = new List<string>();
        }

        #endregion

        #region Localization

        private string GetMessage(string key, IPlayer player = null) =>
            lang.GetMessage(key, this, player?.Id);

        #endregion
    }
}
