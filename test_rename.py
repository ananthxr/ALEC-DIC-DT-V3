#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Quick test to verify the renaming logic works"""

import re

# Test data
test_line = "  m_Name: HVAC"
old_name = "HVAC"
new_name = "8ee01920-1073-11f0-94c5-01236d0e69c4"

print("Testing rename logic...")
print(f"Original line: '{test_line}'")
print(f"Old name: '{old_name}'")
print(f"New name: '{new_name}'")
print()

# Pattern that should match
pattern = rf"(  m_Name: ){re.escape(old_name)}$"
print(f"Pattern: {pattern}")
print(f"Match found: {bool(re.search(pattern, test_line))}")

if re.search(pattern, test_line):
    new_line = re.sub(pattern, rf"\g<1>{new_name}", test_line)
    print(f"New line: '{new_line}'")
else:
    print("ERROR: Pattern did not match!")

# Now test with the actual file
print("\n" + "="*70)
print("Testing with actual Unity scene file...")
print("="*70 + "\n")

scene_path = r"Y:\ALEC DIC DT V2\Assets\Scenes\MainScene-001.unity"

try:
    with open(scene_path, 'r', encoding='utf-8') as f:
        content = f.read()

    lines = content.split('\n')

    # Search for HVAC
    for idx, line in enumerate(lines):
        if 'm_Name: HVAC' in line:
            print(f"Found at line {idx + 1}: '{line}'")
            print(f"Line repr: {repr(line)}")

            # Try the pattern
            pattern = rf"(  m_Name: ){re.escape('HVAC')}$"
            if re.search(pattern, line):
                print("✓ Pattern matches!")
            else:
                print("✗ Pattern does NOT match")
                # Try without $ anchor
                pattern2 = rf"(  m_Name: ){re.escape('HVAC')}"
                if re.search(pattern2, line):
                    print("✓ Pattern without $ anchor matches!")
            break

except Exception as e:
    print(f"Error: {e}")
