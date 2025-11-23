from main_pipeline import TennisAnalysisPipeline
import os
import json

def extract_swings_for_annotation(input_video_path, output_dir):
    """
    Process a video to extract swing clips for manual annotation.
    Creates video clips and feature files that you can review and label.
    """
    pipeline = TennisAnalysisPipeline()
    
    os.makedirs(output_dir, exist_ok=True)
    
    print(f"Processing {input_video_path}...")
    
    try:
        results = pipeline.process_video_to_features(input_video_path, output_dir)
        
        print(f"Found {len(results)} swing clips:")
        
        for i, result in enumerate(results):
            video_name = os.path.basename(input_video_path).split('.')[0]
            clip_name = f"{video_name}_swing_{i}"
            
            print(f"  Clip {i+1}: {clip_name}.mp4")
            print(f"    Duration: {result['end_frame'] - result['start_frame']} frames")
            print(f"    Features shape: {result['features'].shape}")
            
            # Create template label file for manual annotation
            template_label = {
                "video_id": clip_name,
                "usta_score": 0.0,  # YOU NEED TO FILL THIS IN
                "swing_type": "unknown",  # forehand/backhand/serve
                "annotator_id": "your_name",
                "confidence": 1.0,
                "notes": "TODO: Watch video and assign USTA score"
            }
            
            label_path = os.path.join(output_dir, f"{clip_name}_label.json")
            with open(label_path, 'w') as f:
                json.dump(template_label, f, indent=2)
        
        print(f"\nNext steps:")
        print(f"1. Review video clips in '{output_dir}'")
        print(f"2. Edit the *_label.json files with USTA scores")
        print(f"3. Run training pipeline on the annotated data")
        
    except Exception as e:
        print(f"Error processing video: {e}")

def batch_extract_swings(input_dir, output_dir):
    """
    Process multiple videos to extract all swing clips for annotation.
    """
    os.makedirs(output_dir, exist_ok=True)
    
    video_extensions = ['.mp4', '.avi', '.mov', '.mkv']

    for filename in os.listdir(input_dir):
        if any(filename.lower().endswith(ext) for ext in video_extensions):
            video_path = os.path.join(input_dir, filename)
            extract_swings_for_annotation(video_path, output_dir)
            print("-" * 50)

def prepare_for_training(annotation_dir):
    """
    Rename completed annotation files for training.
    Call this after you've filled in all the USTA scores.
    """
    template_files = [f for f in os.listdir(annotation_dir) if f.endswith('_label_template.json')]
    
    for template_file in template_files:
        template_path = os.path.join(annotation_dir, template_file)
        final_path = template_path.replace('_template.json', '.json')
        
        with open(template_path, 'r') as f:
            label_data = json.load(f)
        
        if label_data['usta_score'] > 0:  # Check if annotated
            os.rename(template_path, final_path)
            print(f"Prepared: {template_file} → {os.path.basename(final_path)}")
        else:
            print(f"Skipped (not annotated): {template_file}")

if __name__ == "__main__":
    # Step 1: Extract swing clips from your tennis videos
    print("=== Swing Detection for Data Annotation ===")

    # Option A: Single video
    extract_swings_for_annotation('your_tennis_video.mp4', 'swing_clips_to_annotate/')
    
    # Option B: Batch process multiple videos
    # batch_extract_swings('tennis_videos/', 'swing_clips_to_annotate/')
    
    # Step 2: After you manually annotate, prepare for training
    # prepare_for_training('swing_clips_to_annotate/')