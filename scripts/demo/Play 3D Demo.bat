@echo off
title SS12 demo
cd /d "%~dp0"
echo.
echo  ==============================================
echo    SS12 demo
echo    Space Station 14, in 3D, on your computer
echo  ==============================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0play-demo.ps1"
echo.
echo  This window can be closed now.
pause
