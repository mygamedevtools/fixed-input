// @ts-check
// `@type` JSDoc annotations allow editor autocompletion and type checking
// (when paired with `@ts-check`).
// There are various equivalent ways to declare your Docusaurus config.
// See: https://docusaurus.io/docs/api/docusaurus-config

import mgtTheme from '@mygamedevtools/docusaurus-theme';
import prismTheme from './src/prism/mgtPrismTheme.js';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'My Fixed Tick',
  tagline: 'One press, one fixed step.',
  favicon: 'img/mgt/favicon.svg',

  // The design-system theme serves its logo files under /img/mgt/.
  staticDirectories: ['static', mgtTheme.staticDir],

  // Set the production url of your site here
  url: 'https://fixed-input.mygamedevtools.com',
  // Set the /<baseUrl>/ pathname under which your site is served
  // For GitHub pages deployment, it is often '/<projectName>/'
  baseUrl: '',

  // GitHub pages deployment config.
  // If you aren't using GitHub pages, you don't need these.
  organizationName: 'mygamedevtools', // Usually your GitHub org/user name.
  projectName: 'fixed-input', // Usually your repo name.
  trailingSlash: false,

  onBrokenLinks: 'throw',
  // Left at the default 'warn': the installation page intentionally links to
  // hashes (#openupm, #git, ...) that select a tab instead of a heading anchor.
  onBrokenAnchors: 'warn',

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'en',
    locales: ['en', 'pt-BR'],
  },

  markdown: {
    mermaid: true,
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },

  future: {
    v4: true,
    faster: true,
  },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: './sidebars.js',
          editUrl:
            'https://github.com/mygamedevtools/fixed-input/tree/docs/',
          // Point "Edit this page" at the translation rather than the English
          // source when browsing a localized page.
          editLocalizedFiles: true,
          // 1.0.0 has shipped, so it is the default at /docs/.
          //
          // `current` — the docs/ folder everyone edits — is not published while
          // its content is identical to the released snapshot: a version labelled
          // "Unreleased" that reads the same as 1.0.x only makes the dropdown
          // harder to use. Set this back to true when work on the next version
          // starts diverging, and give `current` a label and an unreleased banner
          // again at that point.
          includeCurrentVersion: false,
          lastVersion: '1.0.0',
          versions: {
            '1.0.0': {
              // Minor-level label: patch releases don't change the docs, so
              // this only needs a bump when a new minor ships.
              label: '1.0.x'
            },
          }
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      }),
    ],
  ],

  themes: ['@docusaurus/theme-mermaid'],

  plugins: ['@mygamedevtools/docusaurus-theme'],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      image: 'img/social-card.png',
      navbar: {
        title: 'My Fixed Tick',
        logo: {
          alt: 'My Gamedev Tools',
          src: 'img/mgt/mark-light.svg',
          srcDark: 'img/mgt/mark-dark.svg',
        },
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'documentationSidebar',
            position: 'left',
            label: 'Docs',
          },
          {
            href: 'https://openupm.com/packages/com.mygamedevtools.fixed-input/',
            position: 'left',
            label: 'OpenUPM'
          },
          {
            type: 'docsVersionDropdown',
            position: 'right'
          },
          {
            type: 'localeDropdown',
            position: 'right'
          },
          {
            href: 'https://github.com/mygamedevtools/fixed-input',
            position: 'right',
            className: 'header-github-link',
            'aria-label': 'GitHub Repository',
          },
        ],
      },
      // No `style: 'dark'`: Infima's .footer--dark would override the theme's footer colors,
      // and the theme already keeps the footer ink in both modes.
      footer: {
        links: [
          {
            title: 'Docs',
            items: [
              {
                label: 'Welcome',
                to: '/docs/intro',
              },
              {
                label: 'Installation',
                to: '/docs/getting-started/installation'
              },
              {
                label: 'Basic Usage',
                to: '/docs/getting-started/basic-usage'
              },
            ],
          },
          {
            title: 'Concepts',
            items: [
              {
                label: 'The Input Surface',
                to: '/docs/advanced-usage/input-surface'
              },
              {
                label: 'The Tick',
                to: '/docs/advanced-usage/the-tick'
              },
              {
                label: 'Latency',
                to: '/docs/advanced-usage/latency'
              },
            ],
          },
          {
            title: 'Links',
            items: [
              {
                label: 'GitHub',
                href: 'https://github.com/mygamedevtools/fixed-input'
              },
              {
                label: 'OpenUPM',
                href: 'https://openupm.com/packages/com.mygamedevtools.fixed-input/'
              },
              {
                label: 'Changelog',
                href: 'https://github.com/mygamedevtools/fixed-input/blob/main/CHANGELOG.md'
              }
            ]
          },
          {
            title: 'Support',
            items: [
              {
                label: 'Troubleshooting',
                to: '/docs/troubleshooting'
              },
              {
                label: 'Report an Issue',
                href: 'https://github.com/mygamedevtools/fixed-input/issues/new',
              },
            ],
          },
        ],
        logo: {
          alt: 'My Gamedev Tools',
          // The footer is always ink, so use the dark-background lockup.
          src: '/img/mgt/lockup-horizontal-dark.svg',
          href: 'https://github.com/mygamedevtools',
          height: 36
        },
        copyright: `Copyright © ${new Date().getFullYear()} My Gamedev Tools. Built with Docusaurus.`,
      },
      prism: {
        // Code blocks are ink in both modes (--mgt-color-code-*), so one theme covers both.
        theme: prismTheme,
        darkTheme: prismTheme,
        additionalLanguages: ['csharp', 'bash', 'diff']
      },
      colorMode: {
        // Dark is ink, light is cream; first-time visitors get their OS preference.
        defaultMode: "dark",
        respectPrefersColorScheme: true
      },
      mermaid: {
        theme: { light: "default", dark: "dark" }
      }
    }),
};

export default config;
