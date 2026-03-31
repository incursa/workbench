Bundled CUE CLI assets for Workbench live under `runtimes/<rid>/native/`.

Update flow:

1. Change [`version.txt`](./version.txt) to the pinned upstream tag.
2. Run `pwsh -File scripts/Resolve-Cue.ps1 -PopulateBundledAssets`.
3. Review the refreshed binaries under `runtimes/`.
