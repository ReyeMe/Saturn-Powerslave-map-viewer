# Saturn-Powerslave-map-viewer
Small C# WPF app to open and view Powerslave maps from Sega Saturn version of the game (Does not work with other versions)
Runs kinda slow since its SharpGL with WPF, might try DX next time.

# File format documentation
I tried my best to comment every field in every struct and all of my code so it should be easy to read.
Just look into "Powerslave.cs" and you'll find everything you need there.

# TODO
- [x] Get map to display
- [x] Figure out some basic flags for planes
- [x] Figure out how to get light level of tiled planes
- [ ] Figure out how to get light level of un-tiled planes
- [ ] Fix vertex precision
- [ ] Export to other formats
- [ ] Load textures
- [ ] Load entities
- [ ] Figure out more of the file format