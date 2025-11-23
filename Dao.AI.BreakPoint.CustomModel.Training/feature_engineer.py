import numpy as np

class FeatureEngineer:
    def __init__(self, dt=1/30, conf_thresh=0.2):
        self.dt = dt
        self.conf_thresh = conf_thresh

    def angle_between(self, a, b, c):
        v1 = a - b
        v2 = c - b
        n1 = np.linalg.norm(v1)
        n2 = np.linalg.norm(v2)
        if n1 == 0 or n2 == 0:
            return np.nan
        cosang = np.dot(v1, v2) / (n1 * n2)
        cosang = np.clip(cosang, -1.0, 1.0)
        return np.degrees(np.arccos(cosang))

    def build_frame_features(self, prev2_xy, prev_xy, curr_xy, confs=None):
        joints = [5,6,7,8,9,10,11,12,13,14,15,16]

        vel = np.zeros_like(curr_xy)
        acc = np.zeros_like(curr_xy)
        if prev_xy is not None:
            vel = (curr_xy - prev_xy) / self.dt
        if prev2_xy is not None:
            acc = (curr_xy - 2 * prev_xy + prev2_xy) / (self.dt ** 2)

        feats = []

        for j in joints:
            v = vel[j]
            a = acc[j]
            speed = np.linalg.norm(v)
            acc_mag = np.linalg.norm(a)
            if confs is not None and confs[j] < self.conf_thresh:
                speed = np.nan
                acc_mag = np.nan
            feats.append(speed)
            feats.append(acc_mag)

        try:
            L_shoulder, R_shoulder = 5, 6
            L_elbow, R_elbow = 7, 8
            L_wrist, R_wrist = 9, 10
            L_hip, R_hip = 11, 12
            L_knee, R_knee = 13, 14
            L_ankle, R_ankle = 15, 16

            angle_left_elbow = self.angle_between(curr_xy[L_shoulder], curr_xy[L_elbow], curr_xy[L_wrist])
            angle_right_elbow = self.angle_between(curr_xy[R_shoulder], curr_xy[R_elbow], curr_xy[R_wrist])
            angle_left_shoulder = self.angle_between(curr_xy[L_elbow], curr_xy[L_shoulder], curr_xy[L_hip])
            angle_right_shoulder = self.angle_between(curr_xy[R_elbow], curr_xy[R_shoulder], curr_xy[R_hip])
            angle_left_hip = self.angle_between(curr_xy[L_shoulder], curr_xy[L_hip], curr_xy[L_knee])
            angle_right_hip = self.angle_between(curr_xy[R_shoulder], curr_xy[R_hip], curr_xy[R_knee])
            angle_left_knee = self.angle_between(curr_xy[L_hip], curr_xy[L_knee], curr_xy[L_ankle])
            angle_right_knee = self.angle_between(curr_xy[R_hip], curr_xy[R_knee], curr_xy[R_ankle])
            
            if confs is not None:
                if confs[L_shoulder] < self.conf_thresh or confs[L_elbow] < self.conf_thresh or confs[L_wrist] < self.conf_thresh:
                    angle_left_elbow = np.nan
                if confs[R_shoulder] < self.conf_thresh or confs[R_elbow] < self.conf_thresh or confs[R_wrist] < self.conf_thresh:
                    angle_right_elbow = np.nan
                if confs[L_elbow] < self.conf_thresh or confs[L_shoulder] < self.conf_thresh or confs[L_hip] < self.conf_thresh:
                    angle_left_shoulder = np.nan
                if confs[R_elbow] < self.conf_thresh or confs[R_shoulder] < self.conf_thresh or confs[R_hip] < self.conf_thresh:
                    angle_right_shoulder = np.nan
                if confs[L_shoulder] < self.conf_thresh or confs[L_hip] < self.conf_thresh or confs[L_knee] < self.conf_thresh:
                    angle_left_hip = np.nan
                if confs[R_shoulder] < self.conf_thresh or confs[R_hip] < self.conf_thresh or confs[R_knee] < self.conf_thresh:
                    angle_right_hip = np.nan
                if confs[L_hip] < self.conf_thresh or confs[L_knee] < self.conf_thresh or confs[L_ankle] < self.conf_thresh:
                    angle_left_knee = np.nan
                if confs[R_hip] < self.conf_thresh or confs[R_knee] < self.conf_thresh or confs[R_ankle] < self.conf_thresh:
                    angle_right_knee = np.nan
        except Exception:
            angle_left_elbow = angle_right_elbow = angle_left_shoulder = angle_right_shoulder = np.nan
            angle_left_hip = angle_right_hip = angle_left_knee = angle_right_knee = np.nan

        angle_feats = [
            angle_left_elbow, angle_right_elbow,
            angle_left_shoulder, angle_right_shoulder,
            angle_left_hip, angle_right_hip,
            angle_left_knee, angle_right_knee
        ]
        feats.extend(angle_feats)

        pos_flat = curr_xy.flatten()
        if confs is not None:
            confs_xy = np.repeat(confs, 2)
            pos_flat = np.where(confs_xy < self.conf_thresh, np.nan, pos_flat)
        feats.extend(pos_flat.tolist())

        return np.array(feats, dtype=np.float32)

    def normalize_features(self, features_arr, method='zscore'):
        feats = features_arr.astype(np.float32).copy()
        col_mean = np.nanmean(feats, axis=0)
        col_std = np.nanstd(feats, axis=0)
        inds = np.where(np.isnan(feats))
        if inds[0].size > 0:
            feats[inds] = np.take(col_mean, inds[1])
        col_std_safe = np.where(col_std == 0, 1.0, col_std)
        if method == 'zscore':
            feats = (feats - col_mean) / col_std_safe
        elif method == 'maxabs':
            maxabs = np.max(np.abs(feats), axis=0)
            maxabs_safe = np.where(maxabs == 0, 1.0, maxabs)
            feats = feats / maxabs_safe
        else:
            raise ValueError('Unknown normalization method: ' + str(method))
        return feats, col_mean, col_std_safe

    def extract_features_from_keypoints(self, keypoints_sequence):
        features_list = []
        prev_xy = None
        prev2_xy = None
        
        for curr_xy, confs in keypoints_sequence:
            feats = self.build_frame_features(prev2_xy, prev_xy, curr_xy, confs=confs)
            features_list.append(feats)
            
            prev2_xy = None if prev_xy is None else prev_xy.copy()
            prev_xy = curr_xy.copy()
        
        if len(features_list) > 0:
            features_arr = np.stack(features_list, axis=0)
            return features_arr
        else:
            return np.array([])