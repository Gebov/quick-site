@echo off

@if exist "%ProgramFiles%\MSBuild\12.0\bin" set PATH=%ProgramFiles%\MSBuild\12.0\bin;%PATH%
@if exist "%ProgramFiles(x86)%\MSBuild\12.0\bin" set PATH=%ProgramFiles(x86)%\MSBuild\12.0\bin;%PATH%

rem build
set "output=%cd%\output\all"
msbuild src/QuickSite.sln /t:Clean,Build /p:Configuration=Release,OutDir="%output%"

copy %cd%\output\all\merged\QuickSite.exe %cd%\output\QuickSite.exe

rd %output% /q /s