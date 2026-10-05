# Contribution Guide

Thank you for considering contributing to My Fixed Tick! Whether you're fixing a bug, adding a feature, or improving the documentation, your contributions are greatly appreciated and help make the tool better for everyone.

## How to Contribute

There are several ways you can contribute to My Fixed Tick:

### 1. Reporting Issues

If you encounter any bugs or have feedback on features, please report them by opening an issue. When reporting an issue, please provide as much detail as possible, including:

- Steps to reproduce the bug (if applicable)
- What behavior you expected and what actually happened
- Any relevant error messages or logs, including the package's own `[FixedInput]` and `[MyFixedTick]` diagnostics

### 2. Submitting Code Changes

If you'd like to submit code to fix an issue or add a feature, please follow these steps:

#### Fork the Repository

- Navigate to the repository on GitHub.
- Click the Fork button in the upper-right corner to create a copy of the repository under your GitHub account.

#### Clone Your Fork

Clone your forked repository to your local machine:

```bash
git clone https://github.com/<your-username>/fixed-input.git
```

#### Create a New Branch

Create a new branch for your feature or bug fix:

```bash
git checkout -b feature-name-or-bugfix
```

#### Make Your Changes

Make your changes to the code or documentation. When working on code:

- Follow the existing coding style and conventions.
- Make sure to write unit tests for new features or bug fixes if applicable.

#### Write Conventional Commit Messages

Releases are versioned from commit messages with [semantic-release](https://github.com/semantic-release/semantic-release), using the [Angular convention](https://github.com/angular/angular/blob/main/CONTRIBUTING.md#commit). A `fix:` commit releases a patch, a `feat:` commit releases a minor version, and a `BREAKING CHANGE:` footer releases a major version. Other types, such as `docs:`, `test:`, `ci:` or `chore:`, release nothing.

```bash
git commit -m "fix: keep a buffered intent across a paused frame"
```

#### Create a Pull Request

Open a pull request from your fork to the main repository against the `main` branch. In the PR description, provide a clear explanation of what you've done, the issue it addresses, and any relevant details.

### 3. Improving Documentation

We welcome any improvements to the documentation as well. If you spot a typo or think something could be explained better, feel free to submit a pull request with the suggested changes!

### 4. Feature Requests

If you have an idea for a new feature, open an issue and describe the feature in detail. Be sure to explain why you think it would be valuable for other users. If you plan to implement the feature yourself, let us know in the issue before you start so we can discuss the approach.

## Development Setup

This repository is a Unity project that hosts the package, so the sample and tests have somewhere to run. Open the repository root in Unity 6000.0 or newer.

| Path | |
|---|---|
| `Packages/com.mygamedevtools.fixed-input` | the package itself |
| `Packages/com.mygamedevtools.fixed-input/Samples` | the reference sample, with a MonoBehaviour and an Entities scene |
| `Packages/com.mygamedevtools.fixed-input/Tests` | edit mode and play mode tests |
| `Assets/Tests` | tests for the sample's code, which can't ship with the package |
| `Assets`, `ProjectSettings` | the host project |

`com.unity.entities` is a development dependency of the host project, not of the package. It is there so the optional Entities assembly is compiled and tested; a consumer without Entities installs nothing extra and compiles nothing extra. Before submitting a change that touches the Entities assembly, also check that the package still compiles with Entities removed from `Packages/manifest.json`.

The host project uses the built-in render pipeline, so the sample renders exactly as it ships. The Universal Render Pipeline package stays installed only so the sample's material converter compiles.

Run the tests from `Window > General > Test Runner`, in both `EditMode` and `PlayMode`.

## Code of Conduct

By contributing to this project, you agree to adhere to our [Code of Conduct](https://github.com/mygamedevtools/.github/blob/main/CODE_OF_CONDUCT.md). Please treat everyone with respect and kindness.

## License

By submitting a pull request, you agree that your contributions will be licensed under the same license as the rest of the project ([MIT License](./LICENSE)).
