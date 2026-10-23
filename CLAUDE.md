# ShellBound

## Git

### Git collaboration
- Before declaring 'no conflict' or pushing after a teammate reports lost work, run `git fsck --lost-found`, `git reflog` and `git log --all --oneline --graph -20` and check for dangling/orphaned commits.
- Preserve file encodings (Hebrew text files may be UTF-8 with BOM or Windows-1255). Never re-encode on merge.

## Architecture

### Unity project conventions
- Controller rumble/haptics logic lives in PlayerInputHandler, not AudioManager.
- Uses the new Input System. Don't suggest legacy Input.GetAxis APIs.
- When searching for missing-script references, ignore Packages/ and Library/. Only search Assets/.
- Never call stop/reset methods from every instance's FixedUpdate. Guard with state checks.
