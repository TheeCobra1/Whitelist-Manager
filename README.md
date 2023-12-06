# Whitelist Manager Plugin

## Overview

Welcome to the Whitelist Manager plugin! This tool allows you to easily manage a dynamic whitelist for your Rust server, keeping your server exclusive and secure by controlling who has access.

## Features

- Add and remove players from the whitelist.
- List all whitelisted players.
- Grant `whitelistmanager.bypass` permission to whitelisted players.
- Kick non-whitelisted players upon joining the server.

## Installation

1. Make sure you have Oxide installed on your Rust server.
2. Download the latest release of the Whitelist Manager plugin from the [Releases](https://github.com/your-username/whitelist-manager/releases) page.
3. Place the downloaded .dll file into your server's `oxide/plugins` folder.
4. Configure the plugin as needed by editing the `config/WhitelistManager.json` file.

## Usage

### Commands

This plugin provides chat and console commands using the same syntax. When using a command in chat, prefix it with a forward slash `/`.

- `/whitelist add <player>`: Add a player to the whitelist. (Requires `whitelistmanager.admin` permission)
- `/whitelist remove <player>`: Remove a player from the whitelist. (Requires `whitelistmanager.admin` permission)
- `/whitelist list`: List all whitelisted players.

### Permissions

This plugin uses the permission system:

- `whitelistmanager.admin`: Allows access to administrative commands for managing the whitelist.
- `whitelistmanager.bypass`: Granted to whitelisted players, allowing them to join without being affected by other plugins' restrictions.

## Configuration

You can configure the plugin by editing the `config/WhitelistManager.json` file. Adjust settings to your preferences.

## Support and Issues

If you encounter any issues or have questions, please open an issue on the [GitHub Issues](https://github.com/Cobrakiller456/-whitelist-manager/issues) page of this repository.

## License

This plugin is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

**Note:** Replace `Cobrakiller456` in the links with your GitHub username or organization name when you create your repository.

