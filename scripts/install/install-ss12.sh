#!/bin/sh
# SS12 installer for Linux and macOS.
#   ./install-ss12.sh /path/to/your/server/code/folder
# It shows what it would change first, and asks before changing anything.

here="$(cd "$(dirname "$0")" && pwd)"
tool="$here/ss12"

if [ ! -x "$tool" ]; then
  chmod +x "$tool" 2>/dev/null
fi
if [ ! -x "$tool" ]; then
  echo "I cannot find the 'ss12' program next to this file. Please unzip the whole download first."
  exit 1
fi

target="$1"
if [ -z "$target" ]; then
  printf "Type or paste the path of your server's code folder (the one containing Content.Client): "
  read -r target
fi
if [ -z "$target" ]; then
  echo "No folder given, so nothing was done."
  exit 1
fi

echo
echo "Your folder: $target"
echo
echo "What would you like to do?"
echo "  1  Install 3D (shows what it will change first; nothing changes until you say yes)"
echo "  2  Check an install"
echo "  3  Remove 3D again (puts your files back exactly as they were)"
echo "  4  Update 3D"
echo "  5  Build a package"
echo "  6  Use an AI helper (connect an AI assistant to this tool, for servers with lots of custom code)"
printf "Type a number and press Enter: "
read -r choice
echo

case "$choice" in
  1)
    echo "--- Step 1: looking, not touching ---"
    "$tool" install "$target" --dry-run --report || { echo; echo "The helper stopped and changed nothing. Read the message above."; exit 1; }
    echo
    printf "Go ahead and install 3D? Type y for yes, anything else to stop: "
    read -r go
    [ "$go" = "y" ] || { echo "Okay, nothing was changed."; exit 0; }
    printf "Make 3D mandatory for every player? (y = yes, n = each player can choose, the usual choice): "
    read -r mand
    extra=""
    [ "$mand" = "y" ] && extra="--enforce"
    echo
    echo "--- Step 2: installing ---"
    "$tool" install "$target" $extra --report
    ;;
  2) "$tool" doctor "$target" --report ;;
  3)
    printf "Remove 3D from this folder? Type y for yes: "
    read -r go
    [ "$go" = "y" ] || { echo "Okay, nothing was changed."; exit 0; }
    "$tool" uninstall "$target" --report
    ;;
  4) "$tool" update "$target" --report ;;
  5) "$tool" package "$target" --report ;;
  6)
    cfg="$here/ss12-ai-config.json"
    cat > "$cfg" <<EOF
{
  "mcpServers": {
    "ss12": {
      "command": "$tool",
      "args": ["mcp"]
    }
  }
}
EOF
    echo "An AI assistant can read your server's code, work out how to fit 3D around your changes, and do it"
    echo "for you, asking before it changes anything. It must support \"MCP servers\""
    echo "(for example Claude Desktop, Claude Code, Cursor or VS Code)."
    echo
    echo "I wrote the connection settings to: $cfg"
    echo "Copy everything inside it into your AI assistant's MCP settings, then tell it:"
    echo "  Add 3D to my server code at $target"
    echo "Step by step guide: docs/USE-WITH-AI.md"
    ;;
  *) echo "I did not understand \"$choice\". Nothing was done." ;;
esac
