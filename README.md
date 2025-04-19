# RegEnforcer
Registry monitoring tool that checks to see if keys are modified, alerts the user, and revert changes.

## Overview
There are many features in Windows that can only be enabled through the Windows Registry. These settings can be undone mysteriously by Windows itself, third party tools, or Windows Updates being pushed down. 
The RegEnforcer will watch registry keys and let you know if they have been changed. First, point it at a folder of .reg files. These files can be created from the Registry Editor to save one or more entries.
The tool will show the contents of your .reg files and hilight rows that are not currently set to the expected value and allows you to fix it. The program can add itself to your startup routine so every time
windows starts it will check the values. Sometimes Windows Updates happens on a reboot when the program can't actively be watching. It will then monitor the registry and alert you if any changes to those keys
happen so you can be tipped off to the change and potentially the culprit making the change.

## Development
This program is created in c# using the WPF. The program is created almost exclusively through vibe coding (see https://en.wikipedia.org/wiki/Vibe_coding). Well, except for this readme file. Although, I'll 
use Gramarly to rewrite it. 

## Inspriation
I am a big Windows Weekly fan (https://twit.tv/shows/windows-weekly). And Paul Thurrott has continually hinted that a listener should help him out and write this. It's the least I can do since I didn't join
Club Twit (https://twit.tv/clubtwit). 
