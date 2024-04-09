# NCShark: PCap Packet Logger for Night Crows

![NC_PE](https://github.com/AlSch092/NCShark/assets/94417808/eb842b79-e40a-47c9-8a90-15af04430b99)

## Credits
- AlSch092 @ Github for porting to Night Crows Global
- Diamondo25 @ Github for MapleShark

## What is this?
NCShark is a pcap driver powered packet logging tool made in C# (fork of MapleShark) for the game Night Crows. This program bypasses any anti-cheat mechanisms to bring you ban-free data logging. All game packet payloads are Protobuf structures, which means they must be deserialized in order to properly interpret parameter field values.

## Requirements
- You must have WinPCap drivers installed
- Proxy/VPN must be turned off while using this program (unless you know how to set this up properly)

## How to Use
Open NCShark.exe after ensuring WinPCap drivers are installed. Under File -> NCShark Setup, select your wireless or ethernet interface, and leave the rest of the defaults. Click 'OK' and then enter in-game and if all is correct, a new logging session should be created.

## Features
- Full outbound and inbound data logging
- Ignoring inbound or outbound data for times of high traffic
- Data converter: highlighting bytes with the mouse will display their values as byte, short, int, string, etc on the bottom-left docked pane.
- Pcap session file loading: save and load .pcap files for further inspection

## Limitations
- Data sending is not supported in this project to prevent general abuse towards the game servers
- The program can become overwhelmed with data in areas of high inbound data activity (hundreds of entities moving nearby at once, for example)
- A new session must be entered in-game to begin logging data due to the nature of the game's encryption method
- Inbound packets above a certain length (1452 bytes) are fragmented by the game server and split into multiple chunks, and this program does not repack them into a single packet currently.
- Protobuf contracts from the game are not included in this project, you can fork and add deserialization yourself if needed
