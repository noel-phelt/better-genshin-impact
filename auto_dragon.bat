@echo off
cd /d "%~dp0"
echo [一条龍] 更好的原神を起動しています...

:: ツールを起動し、終了（タスク完了）するまで待機します
start /wait "" "BetterGI.exe" startOneDragon

echo.
echo [一条龍] タスクが正常に終了しました。10秒後にスリープに移行します...
timeout /t 10

:: PCをスリープ状態にします
rundll32.exe powrprof.dll,SetSuspendState 0,1,0
