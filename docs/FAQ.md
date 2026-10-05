# Frequently asked questions

*[Русская версия](ru/FAQ.md)*

### What is this, in one sentence?
A free add-on that lets you walk around Space Station 14 in 3D, in first or third person, instead of looking down from above.

### Is it official?
No. It is a fan project. It is not made by or connected to the Space Station 14 developers.

### Is it safe?
The helper that installs 3D only reads and writes files in the folder you give it, never connects to the internet, and can
undo everything it did. The 3D game itself runs inside the normal Space Station 14 security sandbox, the same one every
server's code runs in. The whole source code is in this repository for anyone to read.

### Does it change how the game plays?
No. The same jobs, rules, items and balance. What changes is how you see the station and which way `W` walks (towards where you look).

### Do I have to install anything to play on a 3D server?
No. Join the server with the normal launcher. The 3D game downloads along with the server's other files.

### Can I still play the flat top-down way?
Yes, on servers that allow it: press `F12`. Servers can make 3D mandatory for living players. Ghosts and admins can always use the flat view.

### Will it run on my computer?
Nearly certainly. It needs a graphics card with OpenGL 3.3 (almost everything since about 2012). Older computers can turn
off the fancy parts one by one (`F11`), and the game does it for you if it notices it is slow.

### Why did the game lower my graphics by itself?
Every round starts on the highest graphics and measures the frame rate for a few seconds. If it is too low, the game
steps down to a lighter preset and tells you. The next round starts on High again and measures again. A preset or effect
you chose yourself in `F11` is never changed by the game, and the automatic step-down can be switched off there.

### Can I turn the minimap off, or make it bigger?
Yes: `M` switches it on and off and `-` makes it small or large. `F11` has a switch and a size slider for it too. It never
shows other players.

### Is there a server I can just try?
Yes: a test server at `lol.ss12.org`. Open the normal launcher, choose Direct Connect and type the address. More on
[ss12.org](https://ss12.org). It is a test server, so it may restart or be down at times.

### Does it work on Linux or Mac?
There is a Linux demo and a Linux server package, but nobody has opened the Linux *game window* yet, so treat Linux as
untested (see [Play the demo](PLAY-THE-DEMO.md)). Mac has no demo and has not been tried.

### Is there a controller or touch version?
No. It is made for keyboard and mouse.

### Why can't I add 3D to my old server?
3D needs features of the game engine that were added in engine version **286**. Servers on older engines have to update
first. This is checked for you: the helper tells you and changes nothing. See [COMPATIBILITY](COMPATIBILITY.md).

### Which servers have been tested?
The code of several of the most-played servers: the main "Wizard's Den" code, Starlight, and a Marine Corps server,
among others. Results are in [COMPATIBILITY](COMPATIBILITY.md).

### Can it be removed again?
Yes, completely: the helper's **Remove 3D** option puts every file back exactly as it was.

### How much does it cost?
Nothing. It is free and open source (MIT licence).

### How can I help?
- Try the demo and tell us what felt wrong (the mouse feel, the speed, anything).
- Test it on your own server and tell us the result, good or bad.
- Share screenshots.
- If you are a programmer, see [FOR-DEVELOPERS](FOR-DEVELOPERS.md).
