# Repository notes

- Open this project with Unity **6000.3.25f1** (`ProjectSettings/ProjectVersion.txt`); package dependencies are declared in `Packages/manifest.json` and resolved in `Packages/packages-lock.json`.
- The configured build scene is `Assets/Scenes/SampleScene.unity` (`ProjectSettings/EditorBuildSettings.asset`).
- Preserve Unity asset `.meta` files when adding, moving, or deleting assets; their GUIDs are referenced by scenes and settings.
- There is no repo-defined build, test, or lint script. Actively use Unity CLI for supported project, asset, editor, build, and test operations (load the `unity-cli` skill first); fall back to manual edits or the Editor UI only when CLI cannot do the job. The Test Framework package is installed.
- Unity-generated `.csproj`/`.sln` files are ignored (`.gitignore`); do not treat them as project sources or edit them by hand.

## Commit messages

- Use the `type: summary` format for the subject, choosing a type that fits the change (for example, `feat`, `fix`, `docs`, or `chore`).
- Leave a blank line after the subject, then summarize changes as bullets.
- Example:

  ```text
  feat: add a new feature

  - Summarize change 1
  - Summarize change 2
  ```
