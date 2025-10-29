@echo off
REM Unity GameObject Renamer - Easy Run Script
REM This batch file runs the Python renamer script

echo ======================================================================
echo Unity GameObject Renamer
echo ======================================================================
echo.

REM Change to the script directory
cd /d "%~dp0"

REM Try to run with python
python rename_unity_objects.py
if %ERRORLEVEL% EQU 0 goto :success

REM If that failed, try python3
python3 rename_unity_objects.py
if %ERRORLEVEL% EQU 0 goto :success

REM If both failed, show error
echo.
echo ERROR: Python is not installed or not in PATH!
echo Please install Python from https://www.python.org/downloads/
echo Make sure to check "Add Python to PATH" during installation.
echo.
goto :end

:success
echo.
echo Script completed! Press any key to exit...
pause >nul
exit /b 0

:end
pause
exit /b 1
