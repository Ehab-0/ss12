# Let an AI assistant do it for you

*[Русская версия](ru/USE-WITH-AI.md)*

Most servers get 3D with one click (see [INSTALL-ON-YOUR-SERVER.md](INSTALL-ON-YOUR-SERVER.md)). But if your server's
code has been changed a lot, the helper may stop and say *"I could not find the place to change"*. That is the moment
to bring in an AI assistant. The helper can talk to AI assistants directly, so the assistant can read your code, work
out how 3D fits around your changes, and make the changes for you.

You do not need to know how to program. You do need an AI assistant that supports **MCP servers** (a standard way to
give an assistant extra tools). Examples: Claude Desktop, Claude Code, Cursor, VS Code with an AI extension.

> **You stay in control.** The assistant can only look until you say yes. Installing, updating and removing 3D all
> start as a *preview* ("dry run") that changes nothing. Nothing is ever uploaded or pushed anywhere by the helper.

## Step 1: connect the assistant to the helper

**Easiest (Windows, Mac, Linux):** run the installer script (`Install 3D.bat` or `install-ss12.sh`), pick your server
folder, and choose **6 Use an AI helper**. It writes a small file called `ss12-ai-config.json` that has the right
settings, with the right path already filled in.

Open that file, copy everything in it, and paste it into your assistant's MCP settings:

| Assistant | Where to paste it |
|-----------|-------------------|
| Claude Desktop | Settings, Developer, Edit Config. Paste into `claude_desktop_config.json`, then restart the app. |
| Cursor | Settings, MCP, Add new MCP server (or edit `.cursor/mcp.json`). |
| VS Code | Create `.vscode/mcp.json` in your server folder (VS Code calls the top key `servers` instead of `mcpServers`). |
| Claude Code | In a terminal: `claude mcp add ss12 -- "C:/path/to/ss12.exe" mcp` |

If you prefer to write it yourself, it looks like this (use the real path to `ss12.exe`, with `/` slashes):

```json
{
  "mcpServers": {
    "ss12": {
      "command": "C:/Tools/ss12/ss12.exe",
      "args": ["mcp"]
    }
  }
}
```

## Step 2: ask

Open your assistant and say something like:

> Add 3D to my Space Station 14 server code at `C:\MyServer\space-station-14`.

(There is also a ready-made prompt called **add_3d_to_my_server** in assistants that show MCP prompts.)

What happens next, in plain words:

1. The assistant **inspects** your code: is the game engine new enough, is your git folder tidy, which of the small
   changes fit automatically, which do not.
2. It **shows you a preview** of what will change and asks if that is okay.
3. For anything that does not fit automatically (because your server is customised there), it reads the exact
   description of what that change must achieve, finds the right spot in your code, and makes the equivalent
   change by hand. It tells you what it did.
4. It **installs** the rest, then **checks** that your server still builds, and fixes build problems it can.
5. It tells you in plain words what to do next.

## What the assistant can do (and cannot)

| Tool the assistant gets | What it does | Changes files? |
|-------------------------|--------------|----------------|
| `inspect_codebase` | Looks at your code and reports what fits and what does not | No |
| `list_edits` / `explain_edit` | Describes each small change to your existing files and why it exists | No |
| `find_edit_location` | Searches your code for where a change belongs | No |
| `check_install` | Checks an existing 3D install is complete (optionally compiles) | No |
| `build_codebase` | Compiles your client and server and explains errors | Only build output |
| `install_3d` / `update_3d` / `uninstall_3d` | Installs, updates or removes 3D | Only when `dry_run=false`; the default is a preview |

It cannot touch the game engine folder (`RobustToolbox`), and the helper never pushes to or contacts any remote.
Everything goes into one git branch (called `3d`) and one commit, so you can review it or throw the branch away.

## Tips

- **Commit or stash your own work first.** The helper wants a tidy git folder so that the result is easy to review.
- **Read the preview** before you say yes. It lists every file.
- **Something odd afterwards?** Choose *Remove 3D* in the installer script: your files come back exactly as before.
- **Still stuck?** Run `Install 3D`, choose *Check an install*, and attach `ss12-report.txt` when you ask for help
  on the project's issue page.

## For the curious

`ss12 mcp` speaks the Model Context Protocol over standard input and output (JSON-RPC 2.0), uses no network, and
exposes the tools above plus two documents (`ss12://guide`, `ss12://edits`) and one prompt. Anything written
to its output is protocol only; human-readable output is returned as tool results.
