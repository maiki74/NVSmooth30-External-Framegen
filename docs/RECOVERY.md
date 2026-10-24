# Recovering a trial

Normal completion asks the selected game to close and waits up to ten seconds. Files are restored only after Windows reports no running process for the selected executable. If the game is still running or Windows denies process inspection, the trial remains unfinished.

1. Close the selected game. Handle any save prompt yourself.
2. Open Framegen DLL Tester.
3. Choose **Restore interrupted test** on the main screen.
4. Check that the app returns to the ready / saved state. The local trial journal must read `Restored`.

The app does not kill a frozen game. An interrupted tester or power loss does not have a background recovery service; recovery happens when you reopen it and choose Restore interrupted test.

**Remove test DLL** also closes / restores the current or interrupted trial, while keeping the current option and saved choice. It removes the tracked test DLL, ASI and companion configuration only when they were absent before the test; existing originals are restored. It does not erase unrelated game DLLs, trial backups or logs newly created by the mod. If Windows briefly holds a file after game exit, restoration retries for up to about 2.8 seconds per file. A persistent failure keeps the recovery journal pending rather than reporting completion.

An explicitly enabled **Keep framegen setup installed** setup stays installed after the app or game exits. This is intentional persistence, not an interrupted test. Reopening recognizes it only when its tracked installed files still match their hashes. Toggle off or use **Remove test DLL** to disable it and restore every tracked file. A changed / missing installed file or partial installation enters Recovery. Keep the trial backups until removal succeeds. DirectX changes also disable and restore the previous setup before rebuilding the sequence.

## Backups and journals

Each `%LOCALAPPDATA%\Framegen DLL Tester\trials\<trial-id>` folder contains:

- `trial.json`: game path, candidate hashes, tracked file names, original attributes / modification times, process observations and result.
- `0.original`, `1.original`, etc.: exact original file bytes for files that existed before the trial.
- `events.log`: timestamps and staging / recovery events.
- `N.changed-<id>` when needed: a tracked file that changed during the trial, preserved before restoration.
- Known-mod `.log.tail` files when present: up to the last 64 KiB from the game directory, potentially including earlier runs.

The journal is saved before the first game-file replacement. Originals are hash checked before staging and restoration. Restoration is repeatable after partial completion. Original bytes, file attributes and last-write timestamps are restored; the backup is not a full filesystem metadata or ACL image.

A missing or changed original backup blocks restoration. Keep the backup folder and error message. Do not replace a backup with an unrelated downloaded file. Use a separately verified original or the game's normal file verification when appropriate, keeping your mod backups.

## What is outside the backup

Only the chosen target and explicit companion files are tracked. Game saves, registry values, driver profiles and new files created by the game / mod are not backed up or reset. A baseline leaves existing mods installed. Keep an independent backup of important data.

In the simple sequence, known inactive proxy DLLs matching the bundled catalog are also backed up and temporarily suspended during an attempt so recognized copies do not compete with the tested proxy. They are restored afterward. Unknown game DLLs / other mods are not automatically removed.
