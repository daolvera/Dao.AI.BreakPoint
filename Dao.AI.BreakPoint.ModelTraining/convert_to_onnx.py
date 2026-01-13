#!/usr/bin/env python3
"""
Convert TorchScript (.pt) phase quality models to ONNX format.

Usage:
    python convert_to_onnx.py [models_directory]
    
If no directory is provided, uses the current directory.

Requirements:
    pip install torch onnx
"""

import sys
import os
import torch

def convert_model(pt_path: str, onnx_path: str):
    """Convert a single TorchScript model to ONNX."""
    print(f"Loading: {pt_path}")
    model = torch.jit.load(pt_path)
    model.eval()
    
    # Input shape: batch_size x 60 features (20 features * 3 stats: mean, std, range)
    dummy_input = torch.randn(1, 60)
    
    print(f"Exporting: {onnx_path}")
    torch.onnx.export(
        model,
        dummy_input,
        onnx_path,
        input_names=['features'],
        output_names=['quality_score'],
        dynamic_axes={
            'features': {0: 'batch_size'},
            'quality_score': {0: 'batch_size'}
        },
        opset_version=13
    )
    print(f"  ✓ Saved: {onnx_path}")

def main():
    # Get directory from args or use current directory
    models_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    
    if not os.path.isdir(models_dir):
        print(f"Error: Directory not found: {models_dir}")
        sys.exit(1)
    
    # Phase model names
    phases = ['prep_quality', 'backswing_quality', 'contact_quality', 'followthrough_quality']
    
    converted = 0
    for phase in phases:
        pt_path = os.path.join(models_dir, f"{phase}.pt")
        onnx_path = os.path.join(models_dir, f"{phase}.onnx")
        
        if os.path.exists(pt_path):
            try:
                convert_model(pt_path, onnx_path)
                converted += 1
            except Exception as e:
                print(f"  ✗ Error converting {phase}: {e}")
        else:
            print(f"  - Skipping {phase}.pt (not found)")
    
    print(f"\nConverted {converted}/{len(phases)} models")

if __name__ == "__main__":
    main()
