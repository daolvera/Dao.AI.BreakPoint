# Fullstack Agent Tasks - BreakPoint MVP

**Goal**: Complete the fullstack pipeline for video upload → analysis → results display with coaching tips

## Project Context

BreakPoint is a tennis swing analysis platform. Users upload videos, the system extracts poses using MoveNet, runs inference with a custom ML model, and provides coaching recommendations via Azure OpenAI.

### Architecture Flow
```
Angular Upload → API (creates AnalysisRequest) → Azure Blob Storage → 
Azure Function (queue trigger) → ML Inference → Save Results → 
Notify User → Display Results with Skeleton Overlay + Coaching Tips
```

### Key Decisions Made
- **Notification**: Use SignalR for real-time updates
- **Skeleton overlay**: Generated server-side (ML Agent handles)
- **LLM**: Azure OpenAI via Semantic Kernel
- **Stroke type**: User provides at upload time (forehand, backhand, serve)

---

## What's Already Working ✅

- Authentication (Google OAuth, JWT tokens)
- Player profile CRUD
- Frontend video upload component with validation
- `AnalysisRequest` and `AnalysisResult` data models
- `AnalysisStatus` enum: Requested, InProgress, Completed, Failed

---

## Tasks

### Phase 1: Video Upload API (Critical)

**1.1 Create `AnalysisController`** (`ApiService/Controllers/`)
- `POST /analysis/upload` - Upload video + stroke type → Blob Storage → Create AnalysisRequest
- `GET /analysis/{id}` - Get status and results
- `GET /analysis/player/{playerId}` - Analysis history

**1.2 Create `IAnalysisService` / `AnalysisService`** (`Services/`)

**1.3 Create `IAnalysisRepository` / `AnalysisRepository`** (`Services/Repositories/`)

**1.4 Azure Blob Storage** - Create `IBlobStorageService` for uploads

### Phase 2: IAnalysisEventService (Critical - Blocks Azure Function)

Implement the existing interface. Azure Function needs it to fetch requests and save results.

### Phase 3: SignalR Notifications

- Add `AnalysisHub` to API
- Create `signalr.service.ts` in Angular
- Notify when analysis completes

### Phase 4: Frontend Pages

- **Dashboard**: Recent analyses with status, upload button
- **Analysis Results**: Score, skeleton overlay, coaching tips
- **Upload Flow**: Connect existing component + stroke type selector

### Phase 5: Semantic Kernel LLM

- Setup Azure OpenAI connection
- Create `ICoachingService`
- Prompt: quality score + stroke type + negative features + player level → drills

---

## File References

| Pattern | Example |
|---------|---------|
| Controller | `ApiService/Controllers/PlayerController.cs` |
| Service | `Services/PlayerService.cs` |
| Repository | `Services/Repositories/PlayerRepository.cs` |
| DTO | `Services/DTOs/PlayerDto.cs` |

---

## Order

1. AnalysisController + Service + Repository
2. IAnalysisEventService implementation
3. Blob Storage integration
4. Frontend upload → API connection
5. Dashboard page
6. SignalR
7. Analysis results page
8. Semantic Kernel coaching
