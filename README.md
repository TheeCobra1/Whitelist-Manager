# Whitelist Manager Plugin

## Overview

Welcome to the Whitelist Manager plugin! This tool enables easy management of a dynamic whitelist for your Rust server. Maintain exclusivity and security by controlling who can access your server.

## Features

- **Dynamic Whitelisting:** Add and remove players from the whitelist effortlessly.
- **Automatic Kicking:** Non-whitelisted players are automatically kicked upon joining.
- **Whitelist Searching:** Search functionality to find players in the whitelist.
- **Paginated Player Listing:** View all whitelisted players in a user-friendly paginated format.
- **Permission Management:** Automatically grant or revoke `whitelistmanager.bypass` to whitelisted players.
- **Enhanced User Messages:** Color-coded and clearer user messages for better interaction.

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
- `/whitelist search <player>`: Search for a player in the whitelist.

### Permissions

- `whitelistmanager.admin`: Access to whitelist management commands.
- `whitelistmanager.bypass`: Granted to whitelisted players to bypass restrictions.

## Support and Issues

For issues or questions, please open an issue on the [GitHub Issues](https://github.com/Cobrakiller456/-whitelist-manager/issues) page.

## License

Licensed under the MIT License. See the [LICENSE]((https://github.com/TheeCobra1/Whitelist-Manager/blob/main/LICENSE)) file for details.
