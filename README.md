# NCShark: PCap Packet Logger for Night Crows

## Credits
- AlSch092 @ Github for porting to Night Crows Global
- Diamondo25 @ Github for MapleShark

## What is this?
NCShark is a pcap driver powered packet logging tool made in C# (fork of MapleShark) for the game Night Crows. This program bypasses any anti-cheat mechanisms to bring you ban-free data logging. All game packet payloads are Protobuf structures, which means they must be deserialized in order to properly interpret parameter field values. The program does not depend on any external custom DLLs.

## Requirements
- You must have WinPCap drivers installed
- Proxy/VPN must be turned off while using this program (unless you know how to set this up properly)

## How to Use
Open NCShark.exe after ensuring WinPCap drivers are installed. Under File -> NCShark Setup, select your wireless or ethernet interface, and leave the rest of the defaults. Click 'OK' and then enter in-game and if all is correct, a new logging session should be created.

## Limitations
- Data sending is not supported in this project to prevent general abuse towards the game servers.
- The program can become overwhelmed with data in areas of high inbound data activity (hundreds of entities moving nearby at once, for example)
- A new session must be entered in-game to begin logging data due to the nature of the game's encryption method

![NC_PE](https://github.com/AlSch092/NCShark/assets/94417808/eb842b79-e40a-47c9-8a90-15af04430b99)
