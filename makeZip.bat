echo PMDDotNET

mkdir output
mkdir output\compiler
mkdir output\player

del /Q .\output\*.*
del /Q .\output\compiler\*.*
del /Q .\output\player\*.*
xcopy .\PMDDotNETConsole.Net4\bin\Release\net472\*.* .\output\compiler\ /E /R /Y /I /K
xcopy .\PMDDotNETPlayer\bin\Release\*.* .\output\player\ /E /R /Y /I /K
del /Q .\output\*.pdb
del /Q .\output\*.config
del /Q .\output\compiler\*.pdb
del /Q .\output\compiler\*.config
del /Q .\output\player\*.pdb
del /Q .\output\player\*.config
del /Q .\output\bin.zip
copy .\CHANGE.txt          .\output\
copy .\README.md           .\output\
copy .\compile.bat         .\output\
copy .\play.bat            .\output\
copy .\removeZoneIdent.bat .\output\

pause
