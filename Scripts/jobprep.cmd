
set JOB_DIR=%AZ_BATCH_NODE_SHARED_DIR%\%AZ_BATCH_JOB_ID%

rem Create job specific folders
mkdir %JOB_DIR%
mkdir %JOB_DIR%\Model
mkdir %JOB_DIR%\Apsim

rem Extract APSIM and the model files to the job specific folder
7za.exe x -y -o%JOB_DIR%\Model model.zip
7za.exe x -y -o%JOB_DIR%\Apsim apsim-*.zip
del model.zip
del apsim-*.zip

rem Remove all unused files for condor
del %JOB_DIR%\Model\*.bat
del %JOB_DIR%\Model\*.simulations
del %JOB_DIR%\Model\Apsim.pbs
del %JOB_DIR%\Model\Apsim.sub
del %JOB_DIR%\Model\CondorApsim.xml



rem Copy 7zip and AzCopy to shared location
copy /Y * %JOB_DIR%