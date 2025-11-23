import tensorflow as tf
import numpy as np
from sklearn.model_selection import train_test_split
import json
import os

class TennisSwingModel:
    def __init__(self, sequence_length=60, feature_dim=58):
        self.sequence_length = sequence_length
        self.feature_dim = feature_dim
        self.model = None
        
    def build_model(self):
        model = tf.keras.Sequential([
            tf.keras.layers.Conv1D(64, 3, activation='relu', input_shape=(self.sequence_length, self.feature_dim)),
            tf.keras.layers.Conv1D(64, 3, activation='relu'),
            tf.keras.layers.Dropout(0.3),
            tf.keras.layers.Conv1D(128, 3, activation='relu'),
            tf.keras.layers.Conv1D(128, 3, activation='relu'),
            tf.keras.layers.Dropout(0.3),
            tf.keras.layers.GlobalMaxPooling1D(),
            tf.keras.layers.Dense(128, activation='relu'),
            tf.keras.layers.Dropout(0.5),
            tf.keras.layers.Dense(64, activation='relu'),
            tf.keras.layers.Dense(1, activation='linear')  # USTA score 1.0-7.0
        ])
        
        model.compile(
            optimizer='adam',
            loss='mse',
            metrics=['mae']
        )
        
        self.model = model
        return model
    
    def prepare_training_data(self, features_list, labels_list):
        X = []
        y = []
        
        for features, label in zip(features_list, labels_list):
            if features.shape[0] >= self.sequence_length:
                X.append(features[:self.sequence_length])
                y.append(label)
            elif features.shape[0] >= self.sequence_length // 2:
                padded_features = np.pad(features, 
                                       ((0, self.sequence_length - features.shape[0]), (0, 0)), 
                                       mode='edge')
                X.append(padded_features)
                y.append(label)
        
        return np.array(X), np.array(y)
    
    def train(self, X, y, validation_split=0.2, epochs=100, batch_size=32):
        X_train, X_val, y_train, y_val = train_test_split(X, y, test_size=validation_split, random_state=42)
        
        callbacks = [
            tf.keras.callbacks.EarlyStopping(patience=10, restore_best_weights=True),
            tf.keras.callbacks.ReduceLROnPlateau(factor=0.5, patience=5)
        ]
        
        history = self.model.fit(
            X_train, y_train,
            validation_data=(X_val, y_val),
            epochs=epochs,
            batch_size=batch_size,
            callbacks=callbacks,
            verbose=1
        )
        
        return history
    
    def predict(self, features):
        if features.shape[0] < self.sequence_length:
            padded_features = np.pad(features, 
                                   ((0, self.sequence_length - features.shape[0]), (0, 0)), 
                                   mode='edge')
            features = padded_features
        elif features.shape[0] > self.sequence_length:
            features = features[:self.sequence_length]
        
        features = np.expand_dims(features, axis=0)
        prediction = self.model.predict(features, verbose=0)
        return float(prediction[0, 0])
    
    def save_model(self, filepath):
        self.model.save(filepath)
    
    def load_model(self, filepath):
        self.model = tf.keras.models.load_model(filepath)

class TrainingPipeline:
    def __init__(self):
        self.model = TennisSwingModel()
        
    def load_training_data(self, data_dir):
        features_list = []
        labels_list = []
        
        for filename in os.listdir(data_dir):
            if filename.endswith('_features.npy'):
                video_id = filename.replace('_features.npy', '')
                features_path = os.path.join(data_dir, filename)
                label_path = os.path.join(data_dir, f'{video_id}_label.json')
                
                if os.path.exists(label_path):
                    features = np.load(features_path)
                    with open(label_path, 'r') as f:
                        label_data = json.load(f)
                    
                    features_list.append(features)
                    labels_list.append(label_data['usta_score'])
        
        return features_list, labels_list
    
    def train_model(self, data_dir, model_output_path='tennis_model.h5'):
        features_list, labels_list = self.load_training_data(data_dir)
        
        if len(features_list) == 0:
            raise ValueError("No training data found")
        
        print(f"Loaded {len(features_list)} training samples")
        
        self.model.build_model()
        
        X, y = self.model.prepare_training_data(features_list, labels_list)
        
        print(f"Training data shape: {X.shape}")
        print(f"Label data shape: {y.shape}")
        
        history = self.model.train(X, y)
        
        self.model.save_model(model_output_path)
        print(f"Model saved to {model_output_path}")
        
        return history