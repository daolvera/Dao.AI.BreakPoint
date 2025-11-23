from movenet_extractor import MovenetExtractor
from feature_engineer import FeatureEngineer
from swing_detector import SwingDetector
from video_processor import VideoProcessor
from training_pipeline import TrainingPipeline
import numpy as np
import os

class TennisAnalysisPipeline:
    def __init__(self, movenet_model_path="Models/movenet_thunder"):
        self.movenet = MovenetExtractor(movenet_model_path)
        self.feature_engineer = FeatureEngineer()
        self.swing_detector = SwingDetector()
        self.video_processor = VideoProcessor()
        
    def process_video_to_features(self, video_path, output_dir=None):
        frames = self.video_processor.load_video(video_path)
        
        keypoints_sequence = self.movenet.extract_keypoints_from_video(frames)
        
        swing_clips = self.swing_detector.extract_swing_clips(frames, keypoints_sequence)
        
        results = []
        for i, clip in enumerate(swing_clips):
            features = self.feature_engineer.extract_features_from_keypoints(clip['keypoints'])
            
            if output_dir:
                video_name = os.path.basename(video_path).split('.')[0]
                clip_name = f"{video_name}_swing_{i}"
                
                np.save(os.path.join(output_dir, f"{clip_name}_features.npy"), features)
                
                self.video_processor.save_video_clip(
                    clip['frames'], 
                    os.path.join(output_dir, f"{clip_name}.mp4")
                )
            
            results.append({
                'features': features,
                'start_frame': clip['start_frame'],
                'end_frame': clip['end_frame'],
                'frames': clip['frames']
            })
        
        return results
    
    def batch_process_videos(self, input_dir, output_dir):
        os.makedirs(output_dir, exist_ok=True)
        
        video_extensions = ['.mp4', '.avi', '.mov', '.mkv']
        processed_count = 0
        
        for filename in os.listdir(input_dir):
            if any(filename.lower().endswith(ext) for ext in video_extensions):
                video_path = os.path.join(input_dir, filename)
                print(f"Processing {filename}...")
                
                try:
                    results = self.process_video_to_features(video_path, output_dir)
                    print(f"  Found {len(results)} swing clips")
                    processed_count += 1
                except Exception as e:
                    print(f"  Error processing {filename}: {e}")
        
        print(f"Processed {processed_count} videos")
    
    def train_model(self, data_dir, model_output_path='tennis_model.h5'):
        pipeline = TrainingPipeline()
        history = pipeline.train_model(data_dir, model_output_path)
        return history

if __name__ == "__main__":
    pipeline = TennisAnalysisPipeline()
    
    # Example usage for batch processing
    # pipeline.batch_process_videos('input_videos/', 'processed_clips/')
    
    # Example usage for training
    # pipeline.train_model('processed_clips/', 'trained_model.h5')