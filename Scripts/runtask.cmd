rem Args
set APSIM_FILE=%1
set SIMULATION=%2

rem These vars are set by the task provider
rem APSIM_STORAGE_CONTAINER_URL
rem APSIM_STORAGE_KEY

set JOB_DIR=%AZ_BATCH_NODE_SHARED_DIR%\%AZ_BATCH_JOB_ID%
set MODEL_DIR=%JOB_DIR%\Model
set APSIM_DIR=%JOB_DIR%\Apsim

rem Add apsim and tools to path
set PATH=%PATH%;%JOB_DIR%;%APSIM_DIR%

rem Copy the APSIM and model files to the working directory because APSIM tends
rem to lock the files preventing concurrent access
robocopy %MODEL_DIR% %AZ_BATCH_TASK_WORKING_DIR% /MIR /XF *.apsim
copy /Y %MODEL_DIR%\%APSIM_FILE% %AZ_BATCH_TASK_WORKING_DIR%

rem Execute APSIM
%APSIM_DIR%\Model\Apsim.exe "%APSIM_FILE%" "Simulation=/simulations/%SIMULATION%" 1> %SIMULATION%_Apsim.out 2>&1

AzCopy.exe /Source:%AZ_BATCH_TASK_WORKING_DIR% /Dest:%APSIM_STORAGE_CONTAINER_URL% /DestKey:%APSIM_STORAGE_KEY% /Pattern:%SIMULATION%*
