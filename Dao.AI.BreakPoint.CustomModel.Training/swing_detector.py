import cv2
import numpy as np
from scipy.signal import find_peaks

class SwingDetector:
    def __init__(self, min_swing_duration=30, max_swing_duration=120):
        self.min_swing_duration = min_swing_duration
        self.max_swing_duration = max_swing_duration
    
    def detect_swings_from_keypoints(self, keypoints_sequence):
        if len(keypoints_sequence) < self.min_swing_duration:
            return []
            
        wrist_positions = []
        for curr_xy, confs in keypoints_sequence:
            wrist_positions.append(curr_xy[10])  # right wrist
        
        wrist_positions = np.array(wrist_positions)
        
        wrist_velocities = np.linalg.norm(np.diff(wrist_positions, axis=0), axis=1)
        
        smooth_velocities = self._smooth_signal(wrist_velocities)
        
        velocity_threshold = np.percentile(smooth_velocities, 75)
        
        high_velocity_frames = smooth_velocities > velocity_threshold
        
        swing_segments = self._find_continuous_segments(high_velocity_frames)
        
        valid_swings = []
        for start, end in swing_segments:
            duration = end - start
            if self.min_swing_duration <= duration <= self.max_swing_duration:
                valid_swings.append((start, end))
        
        return valid_swings
    
    def _smooth_signal(self, signal, window=5):
        if len(signal) < window:
            return signal
        kernel = np.ones(window) / window
        return np.convolve(signal, kernel, mode='same')
    
    def _find_continuous_segments(self, boolean_array):
        segments = []
        start = None
        
        for i, val in enumerate(boolean_array):
            if val and start is None:
                start = i
            elif not val and start is not None:
                segments.append((start, i))
                start = None
        
        if start is not None:
            segments.append((start, len(boolean_array)))
        
        return segments
    
    def extract_swing_clips(self, frames, keypoints_sequence):
        swing_segments = self.detect_swings_from_keypoints(keypoints_sequence)
        
        swing_clips = []
        for start, end in swing_segments:
            swing_frames = frames[start:end]
            swing_keypoints = keypoints_sequence[start:end]
            swing_clips.append({
                'frames': swing_frames,
                'keypoints': swing_keypoints,
                'start_frame': start,
                'end_frame': end
            })
        
        return swing_clips