// Flat config (ESLint v9+). Replaces the old .eslintrc.cjs, which the
// installed eslint@9.x can no longer read - kept equivalent to that
// file's intent (eslint:recommended + typescript-eslint recommended,
// browser globals) rather than introducing new rules as part of this
// migration.
import js from '@eslint/js';
import tseslint from 'typescript-eslint';
import globals from 'globals';

export default tseslint.config(
  { ignores: ['dist'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'module',
      globals: globals.browser,
    },
  },
);
