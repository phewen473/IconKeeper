@echo off
rem build.bat - builds IconKeeper.exe from src\ and puts it in this folder. Needs the .NET SDK (8 or later).
rem Work from the folder this script is in, wherever it is run from:
cd /d "%~dp0"
rem Compile a single framework-dependent exe (runs on the .NET 8 Desktop Runtime) into publish\
dotnet publish src\IconKeeper.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o publish
rem Stop if the compile failed:
if errorlevel 1 goto failed
rem Copy the fresh exe to the top of the project folder:
copy /y publish\IconKeeper.exe IconKeeper.exe >nul
rem Tell the user where it is:
echo Built %cd%\IconKeeper.exe
rem Done:
exit /b 0
rem Failure path - say so and return an error code:
:failed
echo BUILD FAILED
exit /b 1
