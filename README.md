# Ace-Squared
Multiplayer FPS with voxels. (like minecraft with guns)
Unity implementation of a game called "Ace of Spades".
Coded by me except for some included libraries such as Newtonsoft JSON and Tom Weiland's basic client<->server code.

In project there is the client code and dedicated server code. This project was made with older version of Unity but if you know C# you should be able to setup it pretty quickly. There is some not clean code in the repository, I might clean it later.

The project supports using Ace of Spades voxel maps (*.vxl version 1?) (nuketown map works at least)

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
