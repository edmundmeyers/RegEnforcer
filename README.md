# RegEnforcer
Registry monitoring tool that checks to see if keys are modified, alerts the user, and revert changes.

## Overview
Many features in Windows can only be enabled through the Windows Registry. However, these settings can be altered unexpectedly by Windows itself, third-party tools, or Windows Updates. RegEnforcer addresses this issue by monitoring specific registry keys and alerting you to any modifications.

## Development
This program is developed using C# with WPF. Most of the coding was done through a process known as vibe coding (see https://en.wikipedia.org/wiki/Vibe_coding), with the exception of this readme file. I utilized Grammarly to enhance its clarity and correctness.

## Inspriation
As a loyal fan of Windows Weekly (https://twit.tv/shows/windows-weekly), I have taken inspiration from Paul Thurrott, who has frequently suggested that listeners contribute by developing helpful tools. This is my way of giving back, especially since I have not joined Club Twit (https://twit.tv/clubtwit).

## Walkthrough
RegEnforcer adds itself to the system tray barrowing the Registry Editor icon. When you open it, you get the main screen:

![image](https://github.com/user-attachments/assets/58fbfd3a-c85b-482e-94a9-561400d21fc0)

From the File menu, select "Select Folder". RegEnforcer will load the contents of all the files into the window. If any of the registry values are not what is expected, you can click "Fix" and it will update the registry for you.

A timer runs every two seconds checking to see if any of your registry keys have changed. If they do change, an alert is created from the system tray informing you. 
