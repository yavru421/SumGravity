@echo off
title SumGravity RTX Homeslice CLI

echo Launching KoboldCPP Server...
start "" "C:\AI\koboldcpp\launch_sumgravity.bat"

echo Waiting 8 seconds for model to load into GPU...
timeout /t 8 /nobreak > NUL

echo Starting SumGravity CLI...
cd /d "c:\dev\SumGravity"
dotnet run -- --cli
pause
