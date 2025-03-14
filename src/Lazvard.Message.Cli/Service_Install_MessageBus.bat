@echo off
setlocal

REM Define variables
set SERVICE_NAME=_Camco_MessageService
set NSSM_PATH=C:\NSSM\nssm.exe
set EXE_PATH=%~dp0src\Lazvard.Message.Cli.exe
set CONFIG_PATH=%~dp0src\Lazvard.Message.Cli\config.toml

REM Install the service using NSSM
%NSSM_PATH% install %SERVICE_NAME% %EXE_PATH% --config %CONFIG_PATH%

REM Set the service description
%NSSM_PATH% set %SERVICE_NAME% Description "Lazvard Message Service - AMQP Server"

REM Start the service
%NSSM_PATH% start %SERVICE_NAME%

echo Service %SERVICE_NAME% installed and started successfully.
endlocal
pause
