# Tennis Swing Analysis Model Implementation

## Overview
This implementation provides a complete 1D CNN-based system for analyzing tennis swings and providing coaching feedback based on USTA ratings and technique analysis.

## Components Implemented

### 1. SwingModelTrainingService
**Location**: `Dao.AI.BreakPoint.ModelTraining\SwingModelTrainingService.cs`

**Purpose**: Trains a 1D CNN model using processed swing videos with USTA ratings

**Key Features**:
- Processes swing videos with MoveNet pose data
- Extracts 66 features per frame (velocities, accelerations, angles, positions)
- Normalizes sequences to fixed length (90 frames = 3 seconds at 30 FPS)
- Trains model to predict 6 technique scores: overall, shoulder, contact, preparation, balance, follow-through
- Comprehensive data validation and error handling
- Progress logging and training metrics

**Input**: `List<ProcessedSwingVideo>` with USTA ratings
**Output**: Trained TensorFlow model (.h5 file)

### 2. SwingCnnModel
**Location**: `Dao.AI.BreakPoint.ModelTraining\SwingCnnModel.cs`

**Purpose**: Defines the 1D CNN architecture for swing analysis

**Architecture**:
```
Input: (batch_size, 90, 66) - 90 timesteps, 66 features per timestep
? Conv1D(64, kernel=3) + MaxPool + Dropout
? Conv1D(128, kernel=5) + MaxPool + Dropout  
? Conv1D(256, kernel=7) + GlobalAvgPool + Dropout
? Dense(512) + Dropout
? Dense(6, linear) - [overall, shoulder, contact, prep, balance, follow]
```

**Features**:
- Single output model for all technique scores
- Multi-output model option for specialized heads
- Batch normalization and dropout for regularization
- Configurable learning rate and optimization

### 3. SwingPredictionService
**Location**: `Dao.AI.BreakPoint.Services\MoveNet\SwingPredictionService.cs`

**Purpose**: Uses trained model to predict swing technique and generate coaching recommendations

**Capabilities**:
- Loads trained model for inference
- Processes swing data into model-compatible features
- Predicts technique scores (1.0-7.0 USTA scale)
- Generates actionable coaching recommendations
- Confidence scoring based on pose detection quality
- Technique issue identification

### 4. TrainingConfiguration
**Location**: `Dao.AI.BreakPoint.ModelTraining\TrainingConfiguration.cs`

**Purpose**: Configuration class for training parameters

**Features**:
- Sequence length calculation based on swing duration and frame rate
- Configuration validation with detailed error reporting
- Training summary for logging
- Default values optimized for tennis swing analysis

### 5. VectorUtilities (Enhanced)
**Location**: `Dao.AI.BreakPoint.Services\SwingAnalyzer\VectorUtilities.cs`

**Purpose**: Computes joint angles from pose keypoints (already existed, leveraged by new components)

**Provides**: 8 joint angles (elbows, shoulders, hips, knees) with confidence masking

## Training Process Flow

```
1. Input: Videos + USTA Ratings
   ?
2. MoveNet Pose Extraction
   ?  
3. Feature Engineering (66 features/frame)
   - 24 velocity/acceleration values
   - 8 joint angles
   - 34 position coordinates
   ?
4. Sequence Normalization (90 frames)
   ?
5. 1D CNN Training
   ?
6. Model Output: 6 technique scores
```

## Usage Examples

### Training a Model
```csharp
var poseExtractor = new MoveNetPoseFeatureExtractorService();
var trainingService = new SwingModelTrainingService(poseExtractor);

var config = new TrainingConfiguration
{
    VideoDirectory = "training_videos",
    ModelOutputPath = "models/swing_analyzer_v1.h5",
    Epochs = 50,
    BatchSize = 16,
    SequenceLength = 90,
    NumFeatures = 66
};

var modelPath = await trainingService.TrainTensorFlowModelAsync(trainingVideos, config);
```

### Using Trained Model for Prediction
```csharp
var predictionService = new SwingPredictionService(
    "models/swing_analyzer_v1.h5", 
    poseExtractor
);

var prediction = predictionService.PredictSwingTechnique(
    swingData, 
    imageHeight, 
    imageWidth, 
    frameRate
);

var recommendations = predictionService.GenerateCoachingRecommendations(prediction);
```

### Running Training Program
```bash
dotnet run --project Dao.AI.BreakPoint.ModelTraining \
  --video-dir training_videos/ \
  --epochs 50 \
  --output models/swing_analyzer_v1.h5
```

## Model Outputs

The trained model predicts 6 technique scores (1.0-7.0 USTA scale):

1. **Overall Rating**: General swing quality
2. **Shoulder Technique**: Shoulder rotation and positioning
3. **Contact Technique**: Ball contact point consistency
4. **Preparation Technique**: Early preparation and setup
5. **Balance Technique**: Balance throughout swing
6. **Follow-through Technique**: Swing completion and finish

## Coaching Recommendations

The system generates actionable coaching feedback based on technique weaknesses:

- **Technique-specific drills** for identified problem areas
- **Progressive recommendations** based on overall skill level
- **Biomechanical insights** from pose analysis
- **Prioritized feedback** focusing on 3 most important improvements

## Technical Requirements

- **TensorFlow.NET** for model training and inference
- **MoveNet** for pose estimation 
- **66 features per frame** from pose data
- **90-frame sequences** (3 seconds at 30 FPS)
- **USTA ratings 1.0-7.0** for training labels

## Integration Points

This implementation integrates with:
- Existing `MoveNetPoseFeatureExtractorService`
- Existing `ProcessedSwingVideo` data structures  
- Existing `SwingPrediction` and `TechniqueIssues` classes
- Existing training data loading pipeline
- Command-line training program

## Next Steps for Full System

1. **Data Collection**: Gather tennis videos with USTA ratings
2. **MoveNet Integration**: Implement actual MoveNet inference
3. **Model Training**: Train on real tennis swing data
4. **API Integration**: Connect to swing analysis service
5. **UI Integration**: Add prediction results to web interface
6. **Performance Optimization**: Optimize model size and inference speed

The core training and prediction infrastructure is now complete and ready for integration with actual tennis swing data.