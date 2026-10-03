@echo off
setlocal EnableDelayedExpansion
title SS12 installer
cd /d "%~dp0"

set "TOOL=%~dp0ss12.exe"
if not exist "%TOOL%" (
  echo.
  echo  I cannot find ss12.exe next to this file.
  echo  Please unzip the whole download first, then open "Install 3D.bat" from the unzipped folder.
  echo.
  pause
  exit /b 1
)

echo.
echo  ==============================================
echo    SS12 installer
echo    Adds a 3D view to a Space Station 14 server
echo  ==============================================
echo.

set "TARGET=%~1"
if "%TARGET%"=="" (
  echo  A window will open. Pick your server's code folder ^(the one that has "Content.Client" inside^).
  echo.
  for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "Add-Type -AssemblyName System.Windows.Forms; $d = New-Object System.Windows.Forms.FolderBrowserDialog; $d.Description = 'Pick your Space Station 14 server code folder (it contains Content.Client)'; if ($d.ShowDialog() -eq 'OK') { $d.SelectedPath }"`) do set "TARGET=%%I"
)

if "%TARGET%"=="" (
  echo  No folder was picked, so nothing was done.
  echo.
  pause
  exit /b 1
)

echo  Your folder: %TARGET%
echo.
echo  What would you like to do?
echo.
echo    1  Install 3D            ^(shows what it will change first; nothing is changed until you say yes^)
echo    2  Check an install      ^(is everything still in place?^)
echo    3  Remove 3D again       ^(puts your files back exactly as they were^)
echo    4  Update 3D             ^(after you downloaded a newer version of this helper^)
echo    5  Build a package       ^(uses your server's own build tool^)
echo    6  Use an AI helper      ^(connect an AI assistant to this tool, for servers with lots of custom code^)
echo.
set /p "CHOICE=  Type a number and press Enter: "
echo.

if "%CHOICE%"=="1" goto install
if "%CHOICE%"=="2" goto check
if "%CHOICE%"=="3" goto remove
if "%CHOICE%"=="4" goto update
if "%CHOICE%"=="5" goto package
if "%CHOICE%"=="6" goto ai
echo  I did not understand "%CHOICE%". Nothing was done.
goto end

:install
echo  --- Step 1: looking, not touching ---
"%TOOL%" install "%TARGET%" --dry-run --report
if errorlevel 1 (
  echo.
  echo  The helper stopped, and changed nothing. Read the message above. If you need help, send the file
  echo  "ss12-report.txt" ^(it is in your server folder^) with your question.
  goto end
)
echo.
set /p "GO=  Go ahead and install 3D? Type y for yes, anything else to stop: "
if /i not "%GO%"=="y" (
  echo  Okay, nothing was changed.
  goto end
)
set "EXTRA="
set /p "MAND=  Make 3D mandatory for every player? ^(y = yes, n = each player can choose, the usual choice^): "
if /i "%MAND%"=="y" set "EXTRA=--enforce"
echo.
echo  --- Step 2: installing ---
"%TOOL%" install "%TARGET%" %EXTRA% --report
goto end

:check
"%TOOL%" doctor "%TARGET%" --report
goto end

:remove
set /p "GO=  Remove 3D from this folder? Type y for yes: "
if /i not "%GO%"=="y" (
  echo  Okay, nothing was changed.
  goto end
)
"%TOOL%" uninstall "%TARGET%" --report
goto end

:update
"%TOOL%" update "%TARGET%" --report
goto end

:package
"%TOOL%" package "%TARGET%" --report
goto end

:ai
set "CFG=%~dp0ss12-ai-config.json"
powershell -NoProfile -Command "$p = '%TOOL%'.Replace('\','/'); $j = '{' + [char]10 + '  \"mcpServers\": {' + [char]10 + '    \"ss12\": {' + [char]10 + '      \"command\": \"' + $p + '\",' + [char]10 + '      \"args\": [\"mcp\"]' + [char]10 + '    }' + [char]10 + '  }' + [char]10 + '}'; Set-Content -Path '%CFG%' -Value $j -Encoding ASCII"
echo  An AI assistant can read your server's code, work out how to fit 3D around your changes, and do it
echo  for you, asking before it changes anything. You need an AI assistant that supports "MCP servers"
echo  ^(for example Claude Desktop, Claude Code, Cursor or VS Code^).
echo.
echo  I wrote the connection settings to this file:
echo      %CFG%
echo.
echo  Open it, copy everything inside, and paste it into your AI assistant's MCP settings.
echo  Then tell the assistant:  "Add 3D to my server code at %TARGET%"
echo  Step by step guide: docs\USE-WITH-AI.md
goto end

:end
echo.
echo  Done. This window stays open so you can read everything above.
echo.
pause
endlocal
