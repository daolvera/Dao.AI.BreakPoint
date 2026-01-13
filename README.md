# Dao.AI.BreakPoint

BreakPoint.AI is an AI-powered tennis coaching application that analyzes tennis swings and provides personalized feedback to help players improve their technique.

This project showcases enterprise practices in a real application, featuring a custom AI model for tennis swing analysis. Development is streamed live on the [DAO Codes Twitch Stream](https://www.twitch.tv/daolveradev).

## Features

- **AI Swing Analysis**: Upload videos of your tennis swing for automated analysis
- **Pose Estimation**: MoveNet-based pose detection for accurate body tracking
- **Phase Classification**: LSTM-based swing phase detection (preparation, backswing, contact, follow-through)
- **AI Coaching**: Azure OpenAI-powered personalized coaching recommendations
- **Drill Recommendations**: Targeted drills based on identified areas for improvement

## Architecture

The application is built on .NET Aspire, orchestrating multiple services in a distributed architecture.

### Solution Structure

| Project | Description |
|---------|-------------|
| **Dao.AI.BreakPoint.AppHost** | .NET Aspire orchestration host - manages all services and infrastructure |
| **Dao.AI.BreakPoint.ApiService** | ASP.NET Core Web API with controllers, authentication, and SignalR hubs |
| **Dao.AI.BreakPoint.Services** | Business logic layer with services, repositories, DTOs, and AI/ML inference |
| **Dao.AI.BreakPoint.Data** | EF Core data entities and database context |
| **Dao.AI.BreakPoint.Migrations** | Database migration worker service |
| **Dao.AI.BreakPoint.AnalyzerFunction** | Azure Functions project for background swing analysis processing |
| **Dao.AI.BreakPoint.ModelTraining** | ML.NET model training for swing quality prediction |
| **Dao.AI.BreakPoint.ServiceDefaults** | Shared Aspire service configuration and defaults |
| **Dao.AI.BreakPoint.Web** | Angular frontend application |
| **Dao.AI.BreakPoint.Services.Tests** | Unit tests for the services layer |

### Infrastructure

- **Database**: PostgreSQL
- **Blob Storage**: Azure Blob Storage (Azurite emulator for local development)
- **Monitoring**: Azure Application Insights
- **Secrets**: Azure Key Vault
- **Real-time**: SignalR for analysis progress notifications

### API Layer

The API follows a **controller-service-repository** pattern:

- **Controllers**: Handle HTTP requests, authentication, and request validation
- **Services**: Implement business logic and orchestrate operations
- **Repositories**: Abstract data access using EF Core

### AI/ML Pipeline

1. **Video Upload**: User uploads tennis swing video
2. **Pose Estimation**: MoveNet model extracts body keypoints from each frame
3. **Phase Classification**: LSTM model identifies swing phases
4. **Quality Analysis**: ML.NET model evaluates swing quality
5. **Coaching Generation**: Azure OpenAI generates personalized feedback
6. **Drill Recommendation**: System suggests targeted practice drills

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for Angular frontend)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for local infrastructure)

### Running Locally

1. Clone the repository
2. Open the solution in Visual Studio 2022 or later
3. Set `Dao.AI.BreakPoint.AppHost` as the startup project
4. Press F5 to run

The Aspire host will automatically start:
- PostgreSQL database
- Azurite storage emulator
- API service
- Angular frontend (available at http://localhost:3000)
- Azure Functions analyzer

## Contributing

This project is developed live on Twitch. Feel free to watch, learn, and contribute!

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
