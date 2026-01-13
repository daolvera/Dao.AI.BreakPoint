# ML Agent Tasks - BreakPoint MVP

**Goal**: Train per-stroke quality model with attention, generate skeleton overlays, extract feature importance

## Project Context

Uses MoveNet pose estimation + custom ML model. Model predicts quality score (0-100) with attention showing which frames/joints mattered.

### Key Decisions
- **Output**: Quality score 0-100 per stroke
- **Attention**: Temporal (frames) + Joint-level (body parts)
- **Stroke types**: forehand, backhand, serve (provided, not predicted)
- **Overlay**: Server-side with "worst" frame annotations
- **Export**: ONNX

---

## What's Working ✅

- MoveNet ONNX inference (17 keypoints)
- Joint velocity/acceleration/angle calculations
- Basic swing phase detection
- Feature extraction (66 features/frame)
- CNN architecture skeleton

---

## Tasks

### Phase 1: Validate Swing Detection (Critical)

**1.1 Write Tests** (`Services.Tests/SwingAnalyzer/`)
- Single/multi-swing videos
- Phase boundary accuracy

**1.2 Fix `IsRightHandedSwing`** - Currently stubbed, implement properly

### Phase 2: Model Architecture with Attention (Critical)

**Location**: `ModelTraining/SwingCnnModel.cs`

Add:
- Self-attention over frame sequence (temporal)
- Joint-level attention (which body parts)
- Output: score + attention weights

### Phase 3: Training Pipeline

**Label format** (Daniel provides ~15 videos):
```json
[
  { "video_file": "forehand_pro.mp4", "stroke_type": "forehand", "quality_score": 90 }
]
```
All swings in video get same score. Phase detection segments automatically.

**Pipeline**: Video → MoveNet → Segment swings → Extract features → Train

### Phase 4: Train & Export

- Train on labeled data
- Export to ONNX (`Dao.AI.BreakPoint.Models/`)

### Phase 5: Inference Integration

**Location**: `Services/SwingAnalyzer/SwingAnalyzerService.cs`

- Load ONNX model
- Extract feature importance from attention weights
- Map to interpretable features ("elbow angle at contact", etc.)

### Phase 6: Skeleton Overlay (Demo Wow Factor)

Generate annotated frame for "worst" moment:
- Draw skeleton on frame
- Highlight problem joints in red
- Add score/feature text
- Output: PNG image

### Phase 7: Azure Function

Complete pipeline in `AnalyzerFunction/SwingAnalyzer.cs`:
Video → MoveNet → Segment → Inference → Features → Overlay → Save results

---

## File References

| What | Where |
|------|-------|
| MoveNet | `Services/MoveNet/MoveNetInferenceService.cs` |
| Swing detection | `Services/SwingAnalyzer/SwingPreprocessingService.cs` |
| CNN model | `ModelTraining/SwingCnnModel.cs` |
| Azure Function | `AnalyzerFunction/SwingAnalyzer.cs` |

---

## Order

1. Swing detection tests
2. Fix IsRightHandedSwing
3. Add attention to model
4. Training pipeline + label format
5. Train model (when videos ready)
6. Export ONNX
7. Integrate into SwingAnalyzerService
8. Feature importance extraction
9. Skeleton overlay generation
10. Complete Azure Function
