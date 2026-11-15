# Releasing

Both packages are published by [`.github/workflows/publish.yml`](https://github.com/pawel51/Appium.Harness/blob/main/.github/workflows/publish.yml)
when a tag is pushed, through nuget.org **Trusted Publishing**: GitHub hands the workflow a
short-lived token, nuget.org checks it came from this repo and this workflow file, and no API key
is stored anywhere.

| Package | Tag | Version lives in | Changelog |
|---|---|---|---|
| Appium.Harness | `v<version>` | `src/Appium.Harness/Appium.Harness.csproj` | `CHANGELOG.md` |
| Appium.Harness.Maui | `maui-v<version>` | `src/Appium.Harness.Maui/Appium.Harness.Maui.csproj` | `src/Appium.Harness.Maui/CHANGELOG.md` |

## One-time setup

1. **nuget.org — create the policy.** Sign in, open your username menu ▸ **Trusted Publishing** ▸
   **Create**:

   | Field | Value |
   |---|---|
   | Policy name | `Appium.Harness` |
   | Package owner | your account |
   | Repository owner | `pawel51` |
   | Repository | `Appium.Harness` |
   | Workflow file | `publish.yml` — the file name only, not the path |
   | Environment | `nuget` |

   One policy covers both packages, since both are published from this one workflow.

2. **GitHub — store your nuget.org username.** Repo ▸ Settings ▸ Secrets and variables ▸ Actions ▸
   **New repository secret**: name `NUGET_USER`, value your nuget.org **profile name** (shown on
   your profile page — not your email). `NuGet/login` needs it to ask for the token.

3. **GitHub — create the `nuget` environment.** Repo ▸ Settings ▸ Environments ▸ **New
   environment** ▸ `nuget`. Under *Deployment protection rules* tick **Required reviewers** and add
   yourself: a pushed tag then waits for your approval instead of publishing on its own. Optionally
   restrict *Deployment branches and tags* to the tag patterns `v*` and `maui-v*`.

4. **Merge the workflow into `main`** and push it. A tag runs the workflow as it exists *at the
   tagged commit*, so the tag must point at a commit that has this file.

## Each release

1. **Bump and describe.** Set `<Version>` in the package's `.csproj` and add the changelog entry.
   A removal or signature change of public API is a major bump. Commit and push to `main`.

2. **Tag the commit and push the tag:**

   ```sh
   git tag v2.0.0          && git push origin v2.0.0          # Appium.Harness
   git tag maui-v1.0.0     && git push origin maui-v1.0.0     # Appium.Harness.Maui
   ```

3. **Approve the run.** Repo ▸ Actions ▸ *Publish* ▸ **Review deployments** ▸ approve.

4. **Wait for nuget.org.** After the push it validates and indexes the package; it usually shows
   in search within 15–30 minutes. The package page is
   `https://www.nuget.org/packages/Appium.Harness` (or `…/Appium.Harness.Maui`).

## When something goes wrong

- **"Tag … does not match <Version>"** — the run stopped before packing; nothing was published.
  Delete the tag (`git tag -d v2.0.0 && git push origin :refs/tags/v2.0.0`), fix, tag again.
- **The login step fails** — check the policy's repository, workflow file name and environment
  match exactly, and that `NUGET_USER` is your profile name.
- **A run failed after the push, or you re-run it** — `--skip-duplicate` makes a second push of the
  same version a no-op, so re-running is safe.
- **A bad version reached nuget.org** — it cannot be deleted or replaced, only **unlisted**
  (package page ▸ Manage ▸ Listing). Publish a fixed version with a higher number.
- **A payload-format change** touches both packages: release both, harness first.
