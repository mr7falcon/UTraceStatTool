# Data export
`export.bat C:\UnrealInsights.exe C:\MyTrace.utrace` (both are absolute paths) - takes some time.

# Usage

1. `UTraceStatTool.exe process MyTrace-timers.csv MyTrace-timing_events.csv -stats=my_stats`

Here my_stats is a stats filename. If there are no stats yet, they will be created, otherwise - accumulated within the existing file.


2. `UTraceStatTool.exe compare my_stats1.stats my_stats2.stats -d -s=25`

Here -d - dump results to a file (results.txt), -s - show up to 25 the most important changes
Also, the same can be done with inplace stats processing instead of one (or both) of the arguments:

`UTraceStatTool.exe MyTrace1-timers.csv MyTrace1-timing_events.csv my_stats2.stats`

`UTraceStatTool.exe MyTrace1-timers.csv MyTrace1-timing_events.csv MyTrace2-timers.csv MyTrace2-timing_events.csv`


3. `UTraceStatTool.exe analyze MyTrace-timers.csv MyTrace-timing_events.csv -stats=my_stats.stats`

Here analysis is based on my_stats. If not specified, analysis is based on the stats derived from the same recording.

`UTraceStatTool.exe analyze MyTrace-timers.csv MyTrace-timing_events.csv -stats=my_stats.stats -f=1025`

Here -f - specific frame index to be analyzed.

`UTraceStatTool.exe analyze MyTrace-timers.csv MyTrace-timing_events.csv -stats=my_stats.stats -ts=60 -te=120`

Here -ts and -te specify start and end time of the section to analyze
