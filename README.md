# Whitelist Manager Plugin

## Overview

Welcome to the Whitelist Manager plugin! This tool provides efficient management of a dynamic whitelist for your Rust server. It ensures controlled access to your server, maintaining exclusivity and security.

## Features

- **Dynamic Whitelisting:** Easily add or remove players from the whitelist.
- **Automatic Kicking:** Non-whitelisted players are automatically kicked upon joining.
- **Whitelist Searching:** Enhanced search functionality to locate players in the whitelist.
- **Paginated Player Listing:** Displays all whitelisted players in a user-friendly, paginated format.
- **Bulk Operations:** Clear entire whitelist with a single command.
- **Whitelist Statistics:** Quick count of total whitelisted players.
- **Hot Reload:** Reload whitelist from disk without restarting the plugin.
- **Improved Performance:** Optimized data structures and error handling.
- **Localized Messages:** Color-coded and localized user messages for better interaction.

## Installation

1. Ensure Oxide is installed on your Rust server.
2. Download the latest release of the Whitelist Manager plugin from the [Releases](https://umod.org/plugins/wmgDoDQK2Z) page.
3. Place the `.cs` file in your server's `oxide/plugins` directory.

## Usage

### Commands

Use these commands in chat (with a `/` prefix) or in the server console:

- `/whitelist add <player>`: Add a player to the whitelist. Requires `whitelistmanager.admin`.
- `/whitelist remove <player>`: Remove a player from the whitelist. Requires `whitelistmanager.admin`.
- `/whitelist list [page]`: List all whitelisted players, with optional pagination.
- `/whitelist search <term>`: Search for players in the whitelist by partial SteamID match.
- `/whitelist clear`: Clear all players from the whitelist. Requires `whitelistmanager.admin`.
- `/whitelist count`: Display the total number of whitelisted players.
- `/whitelist reload`: Reload the whitelist from disk. Requires `whitelistmanager.admin`.

### Permissions

- `whitelistmanager.admin`: Grants access to whitelist management commands.

## Support and Issues

For support, questions, or to report issues, please open an issue on the [GitHub Issues](https://github.com/Cobrakiller456/-whitelist-manager/issues) page.

## License

This plugin is licensed under the MIT License. For more details, see the [LICENSE](https://github.com/TheeCobra1/Whitelist-Manager/blob/main/LICENSE) file.