@echo off
setlocal

REM Define variables
set SERVICE_NAME=_Camco_MessageService
set NSSM_PATH=C:\NSSM\nssm.exe


REM Install the service using NSSM
%NSSM_PATH% Edit %SERVICE_NAME% 
endlocal
