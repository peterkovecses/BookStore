---
name: git-workflow
description: Follow the BookStore Git workflow for implementing changes, creating issue-numbered branches, writing Conventional Commits, preparing pull requests, and cleaning up the relevant branch when instructed after merge.
---

# Git Workflow

Follow this workflow for every change. Loading this skill is required whenever it is relevant.

## 1. Issue and Branching

- **Issue:** Every change requires a corresponding GitHub issue. Ensure one exists before starting work; create one when necessary and record its GitHub issue number. The GitHub issue number (for example, `#42`) is the sole official identifier; historical identifiers in existing titles need not be changed.
- **Base Branch:** Start each task on a dedicated branch from `master`.
- **Branch Naming:** Include the issue number in the branch name:
  - `bugfix/42-description`
  - `feature/42-description`
- Use `bugfix/` for bug fixes and `feature/` for other changes. Keep descriptions concise and in lowercase kebab-case.
- Issue creation, branching, committing, pushing, and pull request creation are authorized as part of this workflow. Respect higher-priority environment or permission requirements.

## 2. Commits

- **Tests:** Before every commit, run the relevant tests according to the root `AGENTS.md`. If no tests are relevant, such as for a documentation-only change, state this explicitly.
- **Commit Format:** ALWAYS use [Conventional Commits](https://www.conventionalcommits.org/).
- **Examples:** `feat(catalog): add book search filters`, `fix(catalog): handle missing book details`.
- **Language:** Commit messages MUST be in English.
- **Style:** Messages MUST be concise and single-line.

## 3. Local Verification

Before pushing changes or creating a Pull Request:

- **Build:** Always build the solution and ensure there are no build errors.
- **Tests:** Follow the testing strategy defined in the root `AGENTS.md`:
  - Run relevant unit and integration tests for isolated changes.
  - Run all potentially affected tests when changes involve shared components, module boundaries, or cross-cutting concerns.
  - Run the complete test suite for significant changes, especially those affecting architecture, shared infrastructure, or multiple modules.
  - If no tests are relevant, state this explicitly. Visual presentation is verified manually by the user, not by visual tests.
- **Warnings:** Do not introduce new build or test warnings. Investigate and resolve newly introduced warnings before proceeding. Existing unrelated warnings should be reported but do not automatically block the workflow.
- **Failures:** Do not push changes or create a Pull Request if the build or required tests fail.

## 4. Merge Conflict Resolution

- **Before PR Creation:** Before pushing the branch and creating a Pull Request, ensure it is up to date with the latest `master` branch changes. Fetch the latest changes and merge `origin/master` into the feature or bugfix branch.
- **Conflict Resolution:** If merge conflicts occur, resolve them carefully while preserving the intended behavior of both branches. Never blindly accept all incoming or current changes.
- **Verification:** After resolving conflicts, rebuild the solution and rerun all relevant tests according to the testing strategy defined in the root `AGENTS.md`.
- **After PR Creation:** If new changes to `master` cause the Pull Request to become conflicted, update the feature or bugfix branch only when instructed, resolve conflicts, and repeat the required verification.
- **Human Assistance:** If a conflict cannot be resolved confidently without making assumptions about intended behavior, stop and ask the human user for guidance.

## 5. Pushing and Pull Request Creation

Once local verification succeeds:

1. Push the feature or bugfix branch to the remote repository:

   `git push origin <branch-name>`

2. Create a Pull Request targeting `master`.
3. Link the corresponding GitHub issue in the PR description (e.g., `Closes #42`).
4. Provide a concise PR title and description summarizing the changes and relevant verification results.
5. **Squash Merge:** Prefer configuring the Pull Request to use squash merging, if supported by the hosting platform or repository settings.

## 6. Review and Merge Ownership

- **Human Ownership:** The human user is responsible for reviewing and approving the Pull Request.
- **No Automatic Approval:** Do not approve Pull Requests on behalf of the human user.
- **No Automatic Review Agent:** Do not spawn a review agent or initiate an additional review process automatically.
- **Review Requests:** The human user may explicitly request an agent-based review after completing their own review.
- **Merge:** NEVER merge the Pull Request. The human user exclusively owns the merge decision and execution.
- **Wait for Instructions:** After creating the Pull Request, stop the workflow. Review follow-up, conflict resolution, and cleanup happen only on explicit user instruction.

## 7. Post-Merge Cleanup

Only when instructed by the user after the human has merged the Pull Request:

1. Verify that the Pull Request has actually been merged.
2. Delete the remote branch used for this change, if it has not already been deleted.
3. Switch back to `master` and update it:

   ```bash
   git checkout master
   git pull origin master
   ```

4. Delete the local branch used for this change:

   ```bash
   git branch -d <branch-name>
   ```

- **Safety:** NEVER perform cleanup before confirming that the Pull Request has been merged. Do not clean up unrelated branches or changes.
- If the remote branch has already been deleted, continue with the remaining cleanup steps.
- If the local branch has already been deleted, no further action is required for that step.
- If switching to `master` or deleting the local branch is blocked by uncommitted changes, do not discard or overwrite user changes. Report the situation and ask for guidance.
