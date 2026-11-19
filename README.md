# My Fixed Tick Documentation

This branch contains the documentation for **My Fixed Tick**, a Unity package for a deterministic fixed-step tick and a one-shot input intent bound to a single step.
The site is published at [fixed-input.mygamedevtools.com](https://fixed-input.mygamedevtools.com) and built with [Docusaurus](https://docusaurus.io/), using the My Gamedev Tools [design system](https://github.com/mygamedevtools/design-system) theme.

## Prerequisites

To run and build this documentation site locally, you need to have **Node.js 20+** installed.

You can download the latest version of Node.js from [here](https://nodejs.org/en/download/).

## Running the Website Locally

1. Clone the repository (if you haven’t already) and check out the `docs` branch.
2. Install the necessary dependencies:
  ```bash
  npm ci
  ```
3. To run the site locally:
  ```bash
  npm run start
  ```
  This will start a local development server at `http://localhost:3000`.
4. To run the site locally in a different locale (e.g., Português Brasileiro):
  ```bash
  npm run start -- --locale pt-BR
  ```
  This will start the site with the specified locale.

## Where the pages live

The published pages are the **released snapshot** in `versioned_docs/version-<version>/` (and `i18n/pt-BR/docusaurus-plugin-content-docs/version-<version>/` for Portuguese).
`docs/` is the working copy for the next version, and isn't published while `includeCurrentVersion` is `false` in `docusaurus.config.js`.

To fix something in the released docs, edit the versioned file, and make the same change in `docs/` so the next version keeps it.

## Versioning

To start documentation on a new version, run:

```bash
npm run docusaurus docs:version <version>
```

This will copy the latest documentation and place it into the `version-<version>` folder.
You can update the files inside `docs/` to reflect the new documentation version.

Make sure to update `lastVersion` and the version labels in the `docusaurus.config.js` file as well.
The label tracks the minor release (e.g. `1.0.x`), so it only needs a bump when a new minor ships — not on every patch:

```js
const config = {
  presets: [
    [
      ({
        docs: {
          lastVersion: '<version>',
          versions: {
            '<version>': {
              label: '<minor>.x'
            }
          }
        }
      })
    ]
  ]
}
```

## Building the Website Locally

If you want to validate the site build before pushing, run the following command:

```bash
npm run build
```

This will generate the static files in the `build` directory.

## Deployment

The deployment of the website is automated through a **GitHub Actions** workflow. Every time changes are pushed to the `docs` branch, GitHub Actions will automatically build and deploy the updated documentation to **GitHub Pages**.

You don't need to manually deploy the site, as the process is handled by the GitHub Actions pipeline.

## Contributing

Feel free to contribute by opening issues or submitting pull requests with updates to the documentation. Open documentation pull requests against the `docs` branch rather than `main`.
