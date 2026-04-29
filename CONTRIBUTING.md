# Contributing Guidelines

We want to ensure that this Extend app template lives and continues to grow and evolve. We would like to encourage everyone to help and improve this repository by contributing.

## Table of Contents

- [Table of Contents](#table-of-contents)
- [Getting Started](#getting-started)
  - [Requirements](#requirements)
  - [Initial Setup](#initial-setup)
  - [Run the App Locally](#run-the-app-locally)
- [Project Structure](#project-structure)
- [Pull Request](#pull-request)
  - [Pull Request Type](#pull-request-type)
  - [Branching Guidelines](#branching-guidelines)
  - [Pull Request Guidelines](#pull-request-guidelines)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Enhancements](#suggesting-enhancements)
- [Code of Conduct](#code-of-conduct)
- [License](#license)
- [References](#references)

## Getting Started

### Requirements

Here are the required tools to build and run this application:

- [Docker](https://docs.docker.com/get-docker/) with [Docker Compose](https://docs.docker.com/compose/install/)
- Git
- A text editor or IDE of your choice

For language-specific requirements, refer to the [README.md](README.md).

### Initial Setup

1. Fork the repository
2. Clone your fork locally:
   ```sh
   git clone https://github.com/<your-username>/<repo-name>.git
   cd <repo-name>
   ```
3. Set up environment variables by copying the sample `.env` file (if available):
   ```sh
   cp .env.sample .env
   ```

### Run the App Locally

Build and run the application using Docker Compose:

```sh
docker compose up --build
```

For more detailed instructions, refer to the [README.md](README.md).

## Project Structure

Refer to the [README.md](README.md) for a detailed explanation of the project structure and architecture.

## Pull Request

We have precise rules over how our git commit messages and pull requests should be formatted. This leads to more readable messages that are easy to follow when looking through the project history.

### Pull Request Type

There are types when creating a branch or pull request. The type must follow this rule:

- **feat** or **feature**: A new feature
- **fix** or **hotfix**: A bug fix
- **refactor**: A code change that neither fixes a bug nor adds a feature
- **docs**: Documentation only changes
- **style**: Changes that do not affect the meaning of the code (white-space, formatting, etc.)
- **perf**: A code change that improves performance
- **test**: Adding missing or correcting existing tests
- **chore**: Changes to the build process or auxiliary tools and libraries

### Branching Guidelines

Always create a branch from the default branch (`master` or `main`). Before creating the branch, make sure you are on the default branch.

- Sync your branch with the default branch before opening a pull request:
  ```sh
  git checkout master
  git pull
  git checkout your-feature-branch
  git rebase master
  ```
- If there are conflicts during rebase, resolve them, then continue with `git rebase --continue`.

### Pull Request Guidelines

When creating a pull request, please follow this pattern for the title:

```
type(scope): description
```

Example:

```
fix(auth): handle expired token gracefully
docs(readme): update setup instructions
feat(handler): add input validation
```

Additional guidelines:

- Clearly describe what the change does in the PR description
- Link related issues if applicable
- Ensure the app builds and tests pass
- Keep PRs small and focused — one concern per PR

Please note that this repository is maintained on a best-effort basis. Maintainers may take some time to review pull requests, so please be patient. If there is no response after a while, a polite follow-up comment is welcome.

Maintainers may request changes or improvements before merging.

## Reporting Bugs

If you find a bug:

- Check existing issues first to avoid duplicates
- Open a new issue if it hasn't been reported
- Include:
  - Steps to reproduce
  - Expected vs actual behavior
  - Environment details (OS, runtime version, etc.)

## Suggesting Enhancements

- Feature ideas and improvements are welcome!
- Open an issue describing your idea
- Explain the use case and why it's useful
- For larger changes, please discuss first before submitting a PR

## Code of Conduct

Be respectful and constructive. This is a learning-focused repository, and everyone is here to help each other grow.

## License

By contributing, you agree that your contributions will be licensed under the same license as this project.

## References

- [AccelByte Extend Overview](https://docs.accelbyte.io/gaming-services/modules/foundations/extend/)
- [Extend Override](https://docs.accelbyte.io/gaming-services/modules/foundations/extend/override/)
- [Extend Service Extension](https://docs.accelbyte.io/gaming-services/modules/foundations/extend/service-extension/)
- [Extend Event Handler](https://docs.accelbyte.io/gaming-services/modules/foundations/extend/event-handler/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [gRPC Documentation](https://grpc.io/docs/)

If you've got more questions, feel free to open an issue or discussion. Happy contributing!
