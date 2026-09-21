@echo off
setlocal

set "ARM_DIR=%LOCALAPPDATA%\UnityMCP\arm"

if not exist "%ARM_DIR%" mkdir "%ARM_DIR%"

echo armed> "%ARM_DIR%\vrc-upload.arm"

echo VRChat upload armed for 30 minutes.
pause
