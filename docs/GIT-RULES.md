# Git Rules

Follow these eleven rules for every work session and merge.

1. **Start every session from the current `main`.** At the start of every work session, while checked out on your own branch, fetch `main` and merge `main` into your branch before opening the project in the Unity Editor. Run these commands in order:

   ```powershell
   git fetch origin
   git merge origin/main
   ```

2. **Commit only to the branch you own.** Ako owns `feature/vehicle-camera`, Kyuri owns `feature/orders-cargo`, and Zubuhle owns `feature/greybox-ui`. Before the first commit of every work session, run `git branch --show-current` and confirm that its output is your own branch; do not commit to `main` or another team member's branch.

3. **Make small commits.** A small commit changes one system, has a subject line of at most 72 characters that says what changed, and has a body that says why the change was made.

4. **Keep `Library/` out of version control.** The root `.gitignore` is the mechanism that excludes `Library/`. Before every commit, run the following staged-path check and confirm that it returns no output:

   ```powershell
   git diff --cached --name-only | Select-String '^Library/'
   ```

5. **Claim shared scenes and coordinate outside assets.** Before opening a shared scene, announce your claim to the other two team members; hold the claim until you push the scene change and announce its release. While another team member holds the claim, either wait for the release or work in a separate test scene under `Assets/_Project/Scenes`, then combine the work through prefabs stored in `Assets/_Project/Prefabs`. `Assets/_Project/ThirdParty` is the only location for an asset obtained from outside the team, and [docs/CREDITS.md](CREDITS.md) is the attribution record required for marking.

6. **Use the assigned merge owner.** Ako merges into `main`. Kyuri merges into `main` while Ako is unavailable; unavailable means Ako has not replied to a merge request within 24 hours.

7. **Tag every checkpoint build.** Ako tags each checkpoint build with the calendar date of the build. Run these exact commands, replacing `YYYY-MM-DD` with that date:

   ```powershell
   git tag build-YYYY-MM-DD
   git push origin build-YYYY-MM-DD
   ```

   A second checkpoint build on the same calendar date uses `build-YYYY-MM-DD-2`; increment the suffix by one for each further build that date (`-3`, `-4`, and so on), using the same suffixed tag in both commands.

8. **Keep `main` playable.** `main` holds only a version that opens in Unity `6000.5.4f1` and plays with zero Unity console errors.

9. **Complete the pre-merge check.** Before pushing a merge to `main`, the merging owner opens the merged project in Unity `6000.5.4f1`, loads the scene named in `README.md`—`Assets/Scenes/SampleScene.unity`—plays for at least 60 seconds, and confirms that the Unity console reports zero errors.

10. **Keep failed checks local.** If the pre-merge check reports one or more console errors, do not push the merge to `main`. Leave the merge unpushed on a local branch so that no work is lost, and report the failing check to the team member who owns the affected system within 24 hours.

11. **Use Unity Smart Merge for scene and prefab conflicts.** Resolve a merge conflict in a `.unity` or `.prefab` file with the merge tool registered as `unityyamlmerge`. If the conflict remains unresolved after 30 minutes, hand it to the team member who owns the affected system.
