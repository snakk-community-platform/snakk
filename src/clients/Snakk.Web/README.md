# Snakk.Web - Frontend Build Setup

This document explains the npm-based build system for Tailwind CSS, DaisyUI, and HTMX.

## Overview

This project uses npm to manage frontend dependencies and build assets locally instead of relying on external CDNs. This approach provides:

- **Enhanced Security**: No dependency on external CDN availability or integrity
- **Offline Development**: Work without internet connection
- **Supply Chain Control**: Version pinning and reproducible builds
- **Performance**: Locally bundled assets with cache busting

## Dependencies

All frontend dependencies are managed via npm and defined in `package.json`:

- **tailwindcss** (v3.4.19) - Utility-first CSS framework
- **daisyui** (v4.12.14) - Component library for Tailwind
- **htmx.org** (v2.0.4) - HTML-driven interactions
- **postcss** (v8.5.6) - CSS transformation
- **autoprefixer** (v10.4.24) - Browser compatibility

## Setup

### First Time Setup

```bash
cd src/clients/Snakk.Web
npm install
```

This will:
1. Install all dependencies to `node_modules/`
2. Run the `postinstall` script to copy HTMX to `wwwroot/js/`
3. Generate `package-lock.json` for reproducible builds

### Development Workflow

**Option 1: Automatic CSS builds** (Recommended)
```bash
# Build the project - CSS builds automatically via MSBuild target
dotnet build
dotnet run
```

**Option 2: Watch mode for rapid CSS development**
```bash
# In a separate terminal, watch for CSS changes
npm run watch:css

# In your main terminal, run the app
dotnet run
```

### Production Build

```bash
npm run build:css
dotnet publish
```

## Build Process

### Automatic Build Pipeline

The build process is integrated with MSBuild via targets in `Snakk.Web.csproj`:

1. **npm audit** - Scans for security vulnerabilities (fails on high/critical)
2. **Tailwind CSS build** - Compiles `Styles/input.css` → `wwwroot/css/styles.css`
3. **.NET build** - Standard ASP.NET compilation

### Manual Commands

```bash
# Build minified CSS for production
npm run build:css

# Watch CSS for changes (development)
npm run watch:css

# Copy HTMX library to wwwroot
npm run copy:libs

# Security audit
npm audit
npm audit fix  # Auto-fix vulnerabilities when possible
```

## Security Considerations

### Package Integrity

- **package-lock.json**: Ensures reproducible builds with verified package versions
- **npm audit**: Automatically runs during build to detect vulnerabilities
- **.npmrc**: Configures security settings (strict SSL, audit level)

### Vulnerability Management

**Check for vulnerabilities:**
```bash
npm audit
```

**Fix vulnerabilities automatically:**
```bash
npm audit fix
```

**Update dependencies:**
```bash
npm update
npm run build:css  # Rebuild CSS with updated packages
```

### Supply Chain Security

1. **Version Pinning**: Dependencies use specific versions (not ranges) in package-lock.json
2. **Audit on Build**: MSBuild target fails build if high/critical vulnerabilities found
3. **No External CDNs**: All assets served locally (except SignalR)
4. **Content Security Policy**: CSP headers prevent loading unauthorized external resources

### Known Limitations

- **No SRI for local files**: Local files don't use Subresource Integrity (not needed - integrity guaranteed by server)
- **npm registry trust**: We trust the npm registry; consider using a private registry for enhanced security
- **Build-time execution**: npm packages can execute code during install/build (inherent npm risk)

## File Structure

```
src/clients/Snakk.Web/
├── package.json              # npm dependencies and scripts
├── package-lock.json         # Locked dependency versions
├── .npmrc                    # npm security configuration
├── tailwind.config.js        # Tailwind CSS configuration
├── postcss.config.js         # PostCSS configuration
├── Styles/
│   └── input.css             # Source CSS with Tailwind directives
├── wwwroot/
│   ├── css/
│   │   └── styles.css        # Generated CSS (DO NOT EDIT - auto-generated)
│   └── js/
│       └── htmx.min.js       # Bundled HTMX library
└── node_modules/             # npm packages (gitignored)
```

## Troubleshooting

### CSS not updating

1. Rebuild CSS: `npm run build:css`
2. Clear browser cache (Ctrl+Shift+R)
3. Check that `styles.css` has recent timestamp

### Build fails with npm audit errors

```bash
# View detailed vulnerability report
npm audit

# Attempt automatic fix
npm audit fix

# If fix breaks functionality, you may need to:
# 1. Update code to work with newer versions
# 2. Accept risk temporarily (not recommended)
# 3. Find alternative packages
```

### HTMX not found

```bash
# Re-copy HTMX library
npm run copy:libs
```

### node_modules missing after git clone

```bash
npm install  # Installs all dependencies
```

## CI/CD Integration

For GitHub Actions or other CI/CD:

```yaml
- name: Setup Node.js
  uses: actions/setup-node@v4
  with:
    node-version: '20'

- name: Install dependencies
  working-directory: src/clients/Snakk.Web
  run: npm ci --audit  # Fails if vulnerabilities found

- name: Build CSS
  working-directory: src/clients/Snakk.Web
  run: npm run build:css

- name: Build .NET
  run: dotnet build
```

## Maintenance

### Regular Security Updates

**Monthly:**
```bash
npm audit
npm update
npm run build:css
# Test the application
git commit -m "chore: update npm dependencies"
```

### Major Version Updates

When updating major versions (e.g., Tailwind 3 → 4):

1. Read changelog for breaking changes
2. Update package.json versions
3. Run `npm install`
4. Test thoroughly (especially CSS)
5. Update this README if build process changes

## Additional Resources

- [Tailwind CSS Documentation](https://tailwindcss.com/docs)
- [DaisyUI Documentation](https://daisyui.com/docs)
- [HTMX Documentation](https://htmx.org/docs)
- [npm Security Best Practices](https://docs.npmjs.com/packages-and-modules/securing-your-code)
