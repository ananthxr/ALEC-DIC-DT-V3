# Unity GameObject Renamer - Setup & Usage Guide

## Overview

This Python script automates the renaming of Unity GameObjects based on a CSV file mapping. It reads hierarchy information from a CSV and renames child objects to their corresponding Entity IDs in your Unity scene file.

## Prerequisites

### 1. Python Installation

You need Python 3.6 or higher installed on your system.

**Check if Python is installed:**
```bash
python --version
```
or
```bash
python3 --version
```

**If Python is NOT installed:**

- **Windows**: Download from [python.org](https://www.python.org/downloads/)
  - During installation, make sure to check "Add Python to PATH"

- **macOS**:
  ```bash
  brew install python3
  ```

- **Linux**:
  ```bash
  sudo apt-get install python3
  ```

### 2. No Additional Libraries Needed!

The script uses only Python's built-in libraries (`csv`, `re`, `sys`, `os`), so no pip installations are required!

## File Structure

Make sure your project has this structure:
```
Y:\ALEC DIC DT V2\
├── Naminfg.csv                          # Your CSV file with renaming data
├── rename_unity_objects.py              # The Python script
└── Assets\
    └── Scenes\
        └── MainScene-001.unity          # Your Unity scene
```

## CSV File Format

Your CSV file should have this structure:

| Building | Floor | Room | Name | Type | Entity ID |
|----------|-------|------|------|------|-----------|
| DIC/Main | DIC/Main/Ground Floor | Design & Control room | Block 3 PMV Block_... | HVAC | 8ee01920-1073-... |
| DIC/Main | DIC/Main/Mezzanine Floor | Z1-Melvin Office | DIC Mezzanine Block 2_... | Lights | e8494580-dece-... |

- **Building**: Master node in Unity hierarchy
- **Floor**: Floor level child
- **Room**: Room child
- **Name**: Parent GameObject name
- **Type**: Current child GameObject name (to be renamed)
- **Entity ID**: New name for the child GameObject

## How to Run the Script

### Method 1: Basic Usage (Default Paths)

Simply run the script from the project directory:

```bash
python rename_unity_objects.py
```

or

```bash
python3 rename_unity_objects.py
```

The script will automatically look for:
- CSV file: `Naminfg.csv` (in the same directory)
- Unity scene: `Assets/Scenes/MainScene-001.unity`

### Method 2: Custom Paths

You can specify custom paths for the CSV and scene file:

```bash
python rename_unity_objects.py "path/to/your/file.csv" "path/to/your/scene.unity"
```

**Example:**
```bash
python rename_unity_objects.py "C:\MyProject\data.csv" "C:\MyProject\Assets\Scenes\MyScene.unity"
```

### Method 3: Running from Windows Explorer

1. Open Command Prompt in the project folder:
   - Hold `Shift` + Right-click in the folder
   - Select "Open PowerShell window here" or "Open command window here"

2. Run:
   ```bash
   python rename_unity_objects.py
   ```

## What the Script Does

1. **Loads CSV Data**: Reads your CSV file and extracts all renaming operations
2. **Parses Unity Scene**: Opens your Unity scene file (it's a text-based YAML file)
3. **Searches for GameObjects**: Finds each GameObject by name in the hierarchy
4. **Renames Objects**: Changes the `m_Name` field from the old name (Type) to the new name (Entity ID)
5. **Saves Changes**: Writes the modified scene back to disk

## Example Output

```
======================================================================
Unity GameObject Renamer
======================================================================
CSV Path: Y:\ALEC DIC DT V2\Naminfg.csv
Scene Path: Y:\ALEC DIC DT V2\Assets\Scenes\MainScene-001.unity

✓ Loaded 2 rows from CSV

✓ Loaded Unity scene file (1234567 characters)

Processing 2 renaming operations...

[1/2] Processing: Block 3 PMV Block_Design & Control room_HVAC-Design & Control room -> HVAC
  Type to rename: 'HVAC' → '8ee01920-1073-11f0-94c5-01236d0e69c4'
  ✓ Renamed at line 19326:
    Old: m_Name: 'HVAC '
    New: m_Name: 8ee01920-1073-11f0-94c5-01236d0e69c4

[2/2] Processing: DIC Mezzanine Block 2_Z1-Melvin Office_Lights-Z1-Melvin Office L1 -> Lights
  Type to rename: 'Lights' → 'e8494580-dece-11ef-94c5-01236d0e69c4'
  ✓ Renamed at line 7912:
    Old: m_Name: Lights
    New: m_Name: e8494580-dece-11ef-94c5-01236d0e69c4

✓ Successfully renamed 2/2 GameObjects
✓ Unity scene file updated: Y:\ALEC DIC DT V2\Assets\Scenes\MainScene-001.unity

======================================================================
✓ Renaming completed successfully!
======================================================================
```

## Important Notes

### ⚠️ Before Running the Script

1. **Backup Your Scene**: Always make a backup of your Unity scene before running the script
   ```bash
   copy "Assets\Scenes\MainScene-001.unity" "Assets\Scenes\MainScene-001.unity.backup"
   ```

2. **Close Unity**: Make sure Unity Editor is closed or the scene is not loaded, otherwise Unity might overwrite your changes

3. **Use Version Control**: If using Git, commit your changes before running the script so you can revert if needed

### ⚠️ After Running the Script

1. **Open Unity**: Launch Unity and open the scene
2. **Verify Changes**: Check that the GameObjects have been renamed correctly in the Hierarchy
3. **Test Your Scene**: Make sure everything still works as expected

## Troubleshooting

### "python: command not found" or "python3: command not found"

**Solution**: Python is not installed or not in your PATH. Follow the installation steps above.

### "CSV file not found"

**Solution**: Make sure `Naminfg.csv` is in the same directory as the script, or provide the full path.

### "Unity scene file not found"

**Solution**: Check that the path to your Unity scene is correct. Use the custom path method if needed.

### "GameObject not found in scene"

**Possible causes:**
- The GameObject name in the CSV doesn't exactly match the name in the Unity scene
- The GameObject might have extra spaces or special characters
- The GameObject might not exist in the scene

**Solution**: Double-check the names in your CSV file match exactly with the Unity scene.

### Script runs but no changes appear in Unity

**Solution**:
1. Close Unity completely and reopen it
2. Right-click the scene file in Unity and select "Reimport"
3. Check if Unity has auto-save enabled which might have reverted changes

## Advanced Usage

### Running for Multiple Scenes

Create a batch script (`rename_all.bat` on Windows):

```batch
@echo off
python rename_unity_objects.py "Naminfg.csv" "Assets\Scenes\Scene1.unity"
python rename_unity_objects.py "Naminfg.csv" "Assets\Scenes\Scene2.unity"
python rename_unity_objects.py "Naminfg.csv" "Assets\Scenes\Scene3.unity"
pause
```

### Automating with Git Hooks

You can set up a Git pre-commit hook to automatically run the script before committing.

## Need Help?

If you encounter any issues:
1. Check the error messages in the console output
2. Verify your CSV file format matches the expected structure
3. Make sure your Unity scene file is not corrupted
4. Check file permissions (read/write access)

## Script Features

✅ Automatic CSV parsing
✅ Safe file handling with error messages
✅ Detailed progress output
✅ Support for special characters in names
✅ Preserves Unity scene formatting
✅ No external dependencies required
✅ Cross-platform (Windows, macOS, Linux)

---

**Version**: 1.0
**Last Updated**: 2025-10-24
