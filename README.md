# Snakk

A modern, performant community platform built with .NET 10 and ASP.NET Core Razor Pages. Snakk enables communities to create organized discussions through a hierarchical structure of hubs and spaces, with built-in moderation, real-time features, and multi-community support.

> **⚠️ Pre-Release Software**
> Snakk is currently in active development and should be considered pre-release/alpha software. While the codebase builds and core functionality works, some features may be incomplete, unstable, or subject to breaking changes. Use in production environments is strongly advised against.

## Features

### Core Functionality
- **Hierarchical Organization**: Communities → Hubs → Spaces → Discussions → Posts
- **Multi-Community Support**: Host multiple communities with custom domains
- **Rich Discussion System**: Threaded discussions with markdown support
- **User Authentication**: OAuth integration (Google, GitHub, Discord)
- **Moderation Tools**: Comprehensive moderation dashboard with role-based permissions
- **Search & Discovery**: Full-text search across discussions and posts
- **Trending Content**: Algorithmic trending discussions, spaces, and contributors

### Performance & Scalability
- **Client-Side Caching**: Smart caching with batch read state updates
- **Output Caching**: Page-level caching with granular invalidation
- **Database Optimization**: PostgreSQL with efficient queries and indexes
- **Real-time Updates**: SignalR integration for live features (planned)

### User Experience
- **Responsive Design**: Mobile-first UI with Tailwind CSS
- **Dark Mode**: System-preference aware theming (planned)
- **Endless Scroll**: Configurable infinite scroll with pagination fallback
- **Smart Navigation**: Resume-reading features with unread tracking

## Technology Stack

### Backend
- **.NET 10** - Latest LTS framework
- **ASP.NET Core** - Web framework with Razor Pages
- **Entity Framework Core 10** - ORM for database access
- **PostgreSQL** - Primary database
- **SignalR** - Real-time communication (planned)

### Frontend
- **Razor Pages** - Server-side rendering
- **Tailwind CSS** - Utility-first CSS framework
- **HTMX** - Modern interactions without heavy JavaScript
- **Vanilla JavaScript** - Lightweight client-side enhancements

### Architecture
- **Clean Architecture** - Separation of concerns with Domain/Application/Infrastructure layers
- **CQRS Pattern** - Command/Query separation for complex operations
- **Repository Pattern** - Abstraction over data access
- **Domain-Driven Design** - Rich domain models with value objects

## Project Structure

```
src/
├── core/
│   ├── Snakk.Api/              # REST API backend
│   ├── Snakk.Application/       # Business logic and use cases
│   ├── Snakk.Domain/            # Domain models and interfaces
│   ├── Snakk.Infrastructure/    # External services implementation
│   ├── Snakk.Infrastructure.Database/  # Database context and migrations
│   └── Snakk.Shared/            # Shared utilities and extensions
└── clients/
    └── Snakk.Web/              # Frontend Razor Pages application

docs/
├── client-caching-guide.md     # Client-side caching implementation
├── MODERATION.MD               # Moderation system documentation
├── PROJECT-STRUCTURE.MD        # Detailed architecture overview
├── REALTIME.MD                 # Real-time features documentation
└── GDRP.MD                     # GDPR compliance notes
```

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL 14+](https://www.postgresql.org/download/)
- Your favorite IDE (Visual Studio 2022, VS Code, Rider)

### Initial Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/snakk.git
   cd snakk
   ```

2. **Configure Database**

   Create a PostgreSQL database:
   ```sql
   CREATE DATABASE snakk;
   CREATE USER snakk WITH PASSWORD 'your_password';
   GRANT ALL PRIVILEGES ON DATABASE snakk TO snakk;
   ```

3. **Configure Application Settings**

   Copy `appsettings.Development.json.example` (if exists) or create:

   `src/core/Snakk.Api/appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "DbConnection": "Host=localhost;Database=snakk;Username=snakk;Password=your_password"
     },
     "Authentication": {
       "Google": {
         "ClientId": "your_google_client_id",
         "ClientSecret": "your_google_client_secret"
       },
       "GitHub": {
         "ClientId": "your_github_client_id",
         "ClientSecret": "your_github_client_secret"
       },
       "Discord": {
         "ClientId": "your_discord_client_id",
         "ClientSecret": "your_discord_client_secret"
       }
     }
   }
   ```

4. **Run Database Migrations**
   ```bash
   cd src/core/Snakk.Infrastructure.Database
   dotnet ef database update
   ```

5. **Start the API**
   ```bash
   cd src/core/Snakk.Api
   dotnet run
   ```
   API will be available at `https://localhost:7291`

6. **Start the Web Client**
   ```bash
   cd src/clients/Snakk.Web
   dotnet run
   ```
   Web app will be available at `https://localhost:7001`

### OAuth Configuration

To enable social authentication, register OAuth applications:

- **Google**: [Google Cloud Console](https://console.cloud.google.com/)
- **GitHub**: [GitHub Developer Settings](https://github.com/settings/developers)
- **Discord**: [Discord Developer Portal](https://discord.com/developers/applications)

Set redirect URIs to: `https://localhost:7291/signin-{provider}` (e.g., `/signin-google`)

## Development

### Building
```bash
dotnet build
```

### Running Tests
```bash
dotnet test
```

### Database Migrations
```bash
# Create new migration
cd src/core/Snakk.Infrastructure.Database
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update
```

### Code Structure Guidelines

- **Domain Layer**: Pure business logic, no dependencies
- **Application Layer**: Use cases, orchestration, DTOs
- **Infrastructure Layer**: External services, database, file system
- **Web Layer**: UI, controllers, pages, minimal logic

See [docs/PROJECT-STRUCTURE.MD](docs/PROJECT-STRUCTURE.MD) for detailed architecture.

## Configuration

Key configuration options in `appsettings.json`:

```json
{
  "Features": {
    "MultiCommunityEnabled": true
  },
  "Trending": {
    "FrontPage": {
      "ShowDiscussions": true,
      "ShowSpaces": true,
      "ShowContributors": true
    }
  },
  "Snakk": {
    "DefaultCommunitySlug": "snakk",
    "PrimaryDomains": ["localhost"],
    "DomainCache": {
      "ExpirationMinutes": 15
    }
  }
}
```

## Contributing

Contributions are welcome! Please read our contributing guidelines (coming soon) before submitting pull requests.

### Development Workflow
1. Create a feature branch from `main`
2. Make your changes with clear commit messages
3. Write tests for new functionality
4. Submit a pull request with description of changes

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Roadmap

- [ ] Real-time notifications with SignalR
- [ ] Full-text search with Elasticsearch
- [ ] Email notifications
- [ ] API rate limiting
- [ ] Mobile apps (iOS/Android)
- [ ] Admin dashboard
- [ ] Analytics and insights
- [ ] Plugin system
- [ ] Dark mode support
- [ ] Localization/i18n

## Support

For questions, issues, or feature requests, please [open an issue](https://github.com/yourusername/snakk/issues).

---

## Author

**Pål Rune Sørensen Tuv**
Senior Software Engineer / Systems Architect

- GitHub: [https://github.com/paaltuv](https://github.com/paaltuv)
- LinkedIn: [https://www.linkedin.com/in/pål-rune-sørensen-tuv-702412392/](https://www.linkedin.com/in/p%C3%A5l-rune-s%C3%B8rensen-tuv-702412392/)

---

Built with ❤️ using .NET and modern web technologies.
