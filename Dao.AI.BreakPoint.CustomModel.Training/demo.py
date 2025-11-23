from main_pipeline import TennisAnalysisPipeline
import json
import os

def create_sample_label(video_id, usta_score):
    label_data = {
        'video_id': video_id,
        'usta_score': usta_score,
        'swing_type': 'forehand',
        'annotator_id': 'coach_001'
    }
    return label_data

def demo_pipeline():
    pipeline = TennisAnalysisPipeline()
    
    print("=== Tennis Swing Analysis Pipeline Demo ===")
    
    # Step 1: Process a single video
    print("\n1. Processing single video...")
    try:
        results = pipeline.process_video_to_features(
            'sample_swing.mp4',  # Replace with your video
            'demo_output/'
        )
        print(f"Found {len(results)} swing clips")
        for i, result in enumerate(results):
            print(f"  Clip {i}: {result['features'].shape[0]} frames, {result['features'].shape[1]} features")
    except Exception as e:
        print(f"Error: {e}")
    
    # Step 2: Create sample labels for training
    print("\n2. Creating sample labels...")
    os.makedirs('demo_output/', exist_ok=True)
    
    sample_labels = [
        ('sample_swing_swing_0', 5.2),
        ('sample_swing_swing_1', 4.8),
    ]
    
    for video_id, score in sample_labels:
        label = create_sample_label(video_id, score)
        with open(f'demo_output/{video_id}_label.json', 'w') as f:
            json.dump(label, f)
    
    # Step 3: Train model (only if you have labeled data)
    print("\n3. Training model...")
    try:
        history = pipeline.train_model('demo_output/', 'demo_model.h5')
        print("Training completed!")
    except Exception as e:
        print(f"Training error (expected if no data): {e}")

if __name__ == "__main__":
    demo_pipeline()