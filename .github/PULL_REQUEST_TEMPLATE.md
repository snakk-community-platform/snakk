## Summary

<!-- What does this PR change and why? One or two sentences is usually enough. -->

## Related Issue

<!-- Link any related issue: "Fixes #123" or "Part of #456". Delete this section if not applicable. -->

## Type of Change

- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that changes existing behavior)
- [ ] Refactor (no functional change)
- [ ] Performance improvement
- [ ] Documentation
- [ ] Build / tooling / CI

## Test Plan

<!-- How did you verify the change? List the steps a reviewer could follow to confirm. -->

- [ ]
- [ ]

## Screenshots / Recordings

<!-- Required for UI changes. Drag images/gifs into this section. Delete if not applicable. -->

## Checklist

- [ ] I have read [CONTRIBUTING.md](../CONTRIBUTING.md).
- [ ] Existing tests pass locally (`dotnet run` in the affected test project, or `dotnet test` at the solution root).
- [ ] I added tests for the new behavior or a regression test for the bug.
- [ ] The build produces zero warnings.
- [ ] I did not introduce direct `/api/*` calls from browser JavaScript (BFF pattern respected).
- [ ] I did not expose internal integer IDs in DTOs or gRPC messages (use `PublicId`).
- [ ] I did not edit compiled output under `wwwroot/css/dist/`, `wwwroot/js/dist/`, or `wwwroot/css/vendor/`.
- [ ] I did not commit secrets (`.env`, credentials, tokens, OAuth client secrets).
- [ ] If I changed a DB entity, I generated an EF migration.
- [ ] If I touched a public API, proto, or BFF contract, I updated the relevant callers.

## Notes for Reviewers

<!-- Anything reviewers should know: tricky bits, migration concerns, deferred follow-ups, open questions. Delete if not applicable. -->
