# Architecture

Layer responsibilities:

- Core = what the world is
- Sim = how baseball behaves
- Application = what the user can do
- Infrastructure = how the game stores and loads
- Game = how everything is presented and controlled

## Notes

- Keep MonoGame-specific code isolated to the Game project.
- Use one MonoGame DesktopGL executable with shared class libraries.
