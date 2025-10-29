#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Unity GameObject Renamer Script
This script reads a CSV file with GameObject hierarchy information and renames
child objects in a Unity scene file based on Entity ID mappings.

CSV Format:
Building, Floor, Room, Name, Type, Entity ID
"""

import csv
import re
import sys
import os
from typing import List, Dict, Tuple, Optional

# Fix Windows console encoding issues
if sys.platform == 'win32':
    sys.stdout.reconfigure(encoding='utf-8')


class UnityObjectRenamer:
    def __init__(self, csv_path: str, scene_path: str):
        """
        Initialize the renamer with paths to CSV and Unity scene.

        Args:
            csv_path: Path to the CSV file containing renaming data
            scene_path: Path to the Unity scene file (.unity)
        """
        self.csv_path = csv_path
        self.scene_path = scene_path
        self.renaming_data: List[Dict[str, str]] = []

    def load_csv_data(self) -> bool:
        """
        Load and parse the CSV file.

        Returns:
            True if successful, False otherwise
        """
        try:
            with open(self.csv_path, 'r', encoding='utf-8') as f:
                reader = csv.DictReader(f)
                for row in reader:
                    # Skip empty rows
                    if not any(row.values()):
                        continue

                    # Validate required fields
                    if row.get('Name') and row.get('Type') and row.get('Entity ID'):
                        self.renaming_data.append({
                            'building': row.get('Building', '').strip(),
                            'floor': row.get('Floor', '').strip(),
                            'room': row.get('Room', '').strip(),
                            'name': row.get('Name', '').strip(),
                            'type': row.get('Type', '').strip(),
                            'entity_id': row.get('Entity ID', '').strip()
                        })

            print(f"✓ Loaded {len(self.renaming_data)} rows from CSV")
            return len(self.renaming_data) > 0

        except FileNotFoundError:
            print(f"✗ Error: CSV file not found at {self.csv_path}")
            return False
        except Exception as e:
            print(f"✗ Error reading CSV: {e}")
            return False

    def find_gameobject_by_name(self, scene_content: str, object_name: str) -> Optional[Tuple[int, int]]:
        """
        Find a GameObject by name in the Unity scene content.

        Args:
            scene_content: The full Unity scene file content
            object_name: The name to search for

        Returns:
            Tuple of (start_index, end_index) of the GameObject block, or None if not found
        """
        # Escape special regex characters in the object name
        escaped_name = re.escape(object_name)

        # Search for the GameObject with this name
        # Unity format: m_Name: <name> or m_Name: '<name>' or m_Name: "<name>"
        pattern = rf"m_Name:\s*['\"]?{escaped_name}\s*['\"]?\s*$"

        match = re.search(pattern, scene_content, re.MULTILINE)
        if match:
            return match.start(), match.end()
        return None

    def find_child_gameobject(self, scene_content: str, parent_name: str, child_name: str) -> Optional[Tuple[int, str]]:
        """
        Find a child GameObject under a specific parent in the Unity scene.

        Args:
            scene_content: The full Unity scene file content
            parent_name: The parent GameObject name
            child_name: The child GameObject name to find

        Returns:
            Tuple of (line_number, full_line_content) or None if not found
        """
        lines = scene_content.split('\n')

        # First, find the parent GameObject
        parent_pattern = rf"m_Name:\s*['\"]?{re.escape(parent_name)}\s*['\"]?"
        parent_found = False
        parent_line_idx = -1

        for idx, line in enumerate(lines):
            if re.search(parent_pattern, line):
                parent_found = True
                parent_line_idx = idx
                print(f"  ✓ Found parent '{parent_name}' at line {idx + 1}")
                break

        if not parent_found:
            print(f"  ✗ Parent GameObject '{parent_name}' not found in scene")
            return None

        # Now search for the child GameObject
        # In Unity scenes, child references are typically within a reasonable distance from parent
        # We'll search the entire file for the child name
        child_pattern = rf"m_Name:\s*['\"]?{re.escape(child_name)}\s*['\"]?"

        for idx, line in enumerate(lines):
            if re.search(child_pattern, line):
                print(f"  ✓ Found child '{child_name}' at line {idx + 1}")
                return idx, line

        print(f"  ✗ Child GameObject '{child_name}' not found in scene")
        return None

    def rename_gameobject(self, scene_content: str, old_name: str, new_name: str) -> Tuple[str, bool]:
        """
        Rename a GameObject in the Unity scene content.

        Args:
            scene_content: The full Unity scene file content
            old_name: Current name of the GameObject
            new_name: New name for the GameObject

        Returns:
            Tuple of (modified_content, success_flag)
        """
        # Strip the old_name to handle any whitespace issues
        old_name_stripped = old_name.strip()

        # Use a simpler approach: direct string search and replace for m_Name lines
        # This handles both \n and \r\n line endings automatically

        # Build search patterns for different Unity name formats
        search_patterns = [
            f"  m_Name: {old_name_stripped}\r\n",  # Windows line ending, no quotes
            f"  m_Name: {old_name_stripped}\n",    # Unix line ending, no quotes
            f"  m_Name: '{old_name_stripped} '\r\n",  # Windows, quoted with trailing space
            f"  m_Name: '{old_name_stripped} '\n",    # Unix, quoted with trailing space
            f"  m_Name: '{old_name_stripped}'\r\n",   # Windows, quoted
            f"  m_Name: '{old_name_stripped}'\n",     # Unix, quoted
            f'  m_Name: "{old_name_stripped}"\r\n',   # Windows, double quoted
            f'  m_Name: "{old_name_stripped}"\n',     # Unix, double quoted
        ]

        replacement_patterns = [
            f"  m_Name: {new_name}\r\n",
            f"  m_Name: {new_name}\n",
            f"  m_Name: '{new_name} '\r\n",
            f"  m_Name: '{new_name} '\n",
            f"  m_Name: '{new_name}'\r\n",
            f"  m_Name: '{new_name}'\n",
            f'  m_Name: "{new_name}"\r\n',
            f'  m_Name: "{new_name}"\n',
        ]

        modified = False
        modified_content = scene_content

        for search_pat, replace_pat in zip(search_patterns, replacement_patterns):
            if search_pat in scene_content:
                modified_content = scene_content.replace(search_pat, replace_pat)

                # Find the line number for reporting
                lines_before = scene_content[:scene_content.find(search_pat)].count('\n')
                line_num = lines_before + 1

                modified = True
                print(f"  ✓ Renamed at line {line_num}:")
                print(f"    Old: m_Name: {old_name_stripped}")
                print(f"    New: m_Name: {new_name}")
                break

        if not modified:
            print(f"  ✗ Could not find GameObject '{old_name_stripped}' to rename")
            # DEBUG: Show what we're looking for
            print(f"    Searching for pattern: '  m_Name: {old_name_stripped}'")

        return modified_content, modified

    def process_renaming(self) -> bool:
        """
        Process all renaming operations from the CSV data.

        Returns:
            True if all operations succeeded, False otherwise
        """
        # Read the Unity scene file
        try:
            with open(self.scene_path, 'r', encoding='utf-8') as f:
                scene_content = f.read()
        except FileNotFoundError:
            print(f"✗ Error: Unity scene file not found at {self.scene_path}")
            return False
        except Exception as e:
            print(f"✗ Error reading Unity scene: {e}")
            return False

        print(f"\n✓ Loaded Unity scene file ({len(scene_content)} characters)")
        print(f"\nProcessing {len(self.renaming_data)} renaming operations...\n")

        success_count = 0
        modified_content = scene_content

        # Process each row from CSV
        for idx, data in enumerate(self.renaming_data, start=1):
            print(f"[{idx}/{len(self.renaming_data)}] Processing: {data['name']} -> {data['type']}")
            print(f"  Type to rename: '{data['type']}' → '{data['entity_id']}'")

            # Perform the rename
            modified_content, success = self.rename_gameobject(
                modified_content,
                data['type'],
                data['entity_id']
            )

            if success:
                success_count += 1
            print()

        # Write back to the scene file if any changes were made
        if success_count > 0:
            try:
                with open(self.scene_path, 'w', encoding='utf-8') as f:
                    f.write(modified_content)
                print(f"✓ Successfully renamed {success_count}/{len(self.renaming_data)} GameObjects")
                print(f"✓ Unity scene file updated: {self.scene_path}")
                return True
            except Exception as e:
                print(f"✗ Error writing Unity scene file: {e}")
                return False
        else:
            print("✗ No GameObjects were renamed")
            return False

    def run(self) -> bool:
        """
        Run the complete renaming process.

        Returns:
            True if successful, False otherwise
        """
        print("=" * 70)
        print("Unity GameObject Renamer")
        print("=" * 70)

        # Load CSV data
        if not self.load_csv_data():
            return False

        # Process renaming
        return self.process_renaming()


def main():
    """Main entry point for the script."""
    # Default paths (relative to script location)
    script_dir = os.path.dirname(os.path.abspath(__file__))
    csv_path = os.path.join(script_dir, "Naminfg.csv")
    scene_path = os.path.join(script_dir, "Assets", "Scenes", "MainScene-001.unity")

    # Allow command-line arguments to override defaults
    if len(sys.argv) > 1:
        csv_path = sys.argv[1]
    if len(sys.argv) > 2:
        scene_path = sys.argv[2]

    print(f"CSV Path: {csv_path}")
    print(f"Scene Path: {scene_path}\n")

    # Create renamer and run
    renamer = UnityObjectRenamer(csv_path, scene_path)
    success = renamer.run()

    print("\n" + "=" * 70)
    if success:
        print("✓ Renaming completed successfully!")
    else:
        print("✗ Renaming failed. Please check the errors above.")
    print("=" * 70)

    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
