@echo off

setlocal

set "filename=%~n2"

echo TimingInsights.ExportTimers %CD%\%filename%-timers.csv > export.rsp
echo TimingInsights.ExportTimingEvents %CD%\%filename%-timing_events.csv >> export.rsp

%1 -OpenTraceFile=%CD%\%2 -AutoQuit -NoUI -ExecOnAnalysisCompleteCmd="@=%CD%\export.rsp"

del export.rsp

endlocal