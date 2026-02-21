# Snakk Database Seeder

A standalone console application for seeding the Snakk database with initial data.

## Features

- Applies pending EF Core migrations automatically
- Seeds lookup tables (UserRoleTypeLookup, CommunityVisibilityLookup)
- Creates default admin user (`admin@snakk.local / admin123`)
- Creates test user for development
- Seeds communities, hubs, spaces, and discussions with realistic data
- Generates SVG avatars for all entities
- Idempotent - can be run multiple times safely

## Prerequisites

- .NET 10 SDK
- PostgreSQL database
- Database user credentials

## Configuration

Update `appsettings.Development.json` with your database connection:

```json
{
  "ConnectionStrings": {
    "DbConnection": "Host=localhost;Database=snakk;Username=snakk;Password=YOUR_PASSWORD"
  }
}
```

## Usage

### Development Environment (Default)

The project includes `launchSettings.json` that sets Development as the default environment:

```bash
cd src/tools/Snakk.DbSeeder
dotnet run
```

Or explicitly specify the environment:

```bash
dotnet run --environment Development
```

### Production Environment

```bash
cd src/tools/Snakk.DbSeeder
dotnet run --launch-profile Production
```

Or:

```bash
dotnet run --environment Production
```

## What Gets Seeded

### Users
- **Admin User**: `admin@snakk.local / admin123` (with GlobalAdmin role)
- **Test User**: `test@snakk.dev` (no password, development only)
- **30+ Sample Users**: Various names for realistic discussions

### Communities
- **Snakk Community**: Main platform community
  - Technology Hub (4 spaces, ~142 discussions)
  - Gaming Hub (3 spaces, ~63 discussions)
  - Science Hub (2 spaces, ~22 discussions)

- **Test1 Community**: Small test community (`test1.snakk.local`)
  - General Hub (2 spaces, ~14 discussions)

- **Test2 Community**: Medium test community (`test2.snakk.local`)
  - Discussion Hub (3 spaces, ~75 discussions)
  - Projects Hub (2 spaces, ~13 discussions)

- **Test3 Community**: Large test community (`test3.snakk.local`)
  - Learning Hub (4 spaces, ~139 discussions)
  - Community Hub (3 spaces, ~53 discussions)
  - Creative Hub (5 spaces, ~163 discussions)

### Data Distribution
- Realistic reply counts (skewed distribution)
- Variable discussion activity
- Pinned/locked discussions (small percentages)
- Time-realistic creation dates

## Database Schema

The seeder automatically runs EF Core migrations before seeding, ensuring your database schema is up to date.

## Troubleshooting

### Connection Errors

If you get password authentication errors:
1. **Check you're running in the correct environment**:
   - Use `dotnet run --environment Development` to load `appsettings.Development.json`
   - Without `--environment`, it defaults to Production which uses `appsettings.json`
2. Check your PostgreSQL connection string in the correct appsettings file
3. Ensure the database exists: `createdb snakk`
4. Ensure the user exists: `createuser -P snakk`

### Missing Tables

The seeder runs migrations automatically. If tables are missing:
1. Check migration files in `Snakk.Infrastructure.Database/Migrations`
2. Ensure EF Core tools are installed: `dotnet tool install --global dotnet-ef`

### Seeding Errors

The seeder is idempotent and safe to re-run. It:
- Skips existing admin/test users
- Checks for existing data before full reseed
- Preserves admin and test users during reseed

## Architecture

- **Standalone Tool**: No dependency on running API
- **Clean Separation**: Seeding logic isolated from application code
- **Dependency Injection**: Uses Microsoft.Extensions.Hosting
- **Configuration**: Standard appsettings.json pattern
