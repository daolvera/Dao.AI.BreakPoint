# Dao.AI.BreakPoint

This project is intended for showcasing enterprise practices in a real application. The central focus of the project is a custom AI model to analyze a tennis swing and give coaching feedback to the user to improve.

## Architecture

The main application is an Aspire application hosting a C# API, an Angular Frontend, and a sql database
This uses standard client server architecture.

### API

The API uses a controller-service-repository pattern to interact with the database and the frontend.
Dao.AI.BreakPoint.Data is the EF Core data entity project. All entities and related objects go here.
Dao.AI.BreakPoint.ApiService holds the controller layer and handles the authentication setup.
Dao.AI.BreakPoint.Services holds the services and repositories and the connecting DTOs and SearchParams objects. Any needed models for frontend communication go here.
Dao.AI.BreakPoint.Web holds the angular front end which is the main desktop application for both users and admins to interact with BreakPoint.AI
