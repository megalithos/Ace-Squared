# Ace-Squared
Multiplayer FPS with voxels (like minecraft with guns) made with Unity & C#
Unity implementation of a game called "Ace of Spades".
Coded by me except for some included libraries such as Newtonsoft JSON and Tom Weiland's basic client<->server code.

# Donate
If the codebase is helpful for you, please consider donating. If you wish to donate please email me.

# Info
See LICENSE files (files in root directory starting with LICENSE prefix).
Originally made with unity 2020.2.3f1, but I just configured the project to work with unity 2021.3.0f1

In project there is the client code and dedicated server code. This project was made with older version of Unity but if you know C# you should be able to setup it pretty quickly. There is some not clean code in the repository, I might clean it later.

The project supports using Ace of Spades voxel maps (.vxl version 1?) (nuketown map works at least)
I decided to include both server & client in the same repo because you will need them both if you want to continue developing it.

Features:
- OK code and pretty fast
- basic lag compensation
- destructible/editable environment
- fall damage, shooting physics, health calculated on the server
- chat, killfeed, scoreboard
- basic admin moderation commands such as ban

This project has been tested with 10 concurrent players and it ran fine (mono build on a linux server).

Here is a demo video:
https://youtu.be/pufQu9REhOo

# Get started quickly
- Resolve any errors (you might get error that newtonsoft is not referenced)
- Make sure URP is installed via package manager
- Open scenes in unitygameserver & gameclient
- Build & run both
- On client type "|127.0.0.1" to username field in server browser
- Click connect on client

# Contact
You can contact me via email islaitala@gmail.com or discord Akseli#9877
