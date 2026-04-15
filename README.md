**The Precision Reference Organizer**  
A grid-based alternative to PureRef that keeps your files organized, local, and always accessible to your creative pipeline.
  
<br>
  
<center>
<img width="1200" height="800" alt="image" src="https://github.com/user-attachments/assets/c8eef5f5-333a-432b-a823-fd8462fe774b" />
</center>
  
<br>
  
# 🛠 Why this exists?
Most reference tools hide your images in proprietary save files. This project is built on the philosophy of **transparency** and **direct accessibility**:
- **Direct HDD Access:** All items are stored right on your drive. Need that image in Maya? Just grab it from the folder or copy the path directly from the app.
- **The "Database" Dir:** Every item is automatically downloaded and organized into a local directory.
- **Json File Format:** *.cgrb files are just JSONs!

<br>
  
# 👋 Getting Started
### 💡 Basic Concept
- 🕹️ **Dual-Mode System:** The application is divided into two primary modes to keep the interface clean and focused:
    - **Grid Mode (Default):** For high-level board management, arranging items, and organizing your reference layout.
    - **Annotation Mode:** For freehand drawing, sketching, and taking notes directly on the board.
    - **Note:** Tools are context-sensitive. Right-click anywhere to access mode-specific actions, such as drawing tools in Annotation mode or backdrop creation in Grid mode.
- 📥 **Universal Input:** Adding references is designed to be as universal as possible:
    - **Drag & Drop:** Pull files directly from your local drive onto the canvas.
    - **Smart Links:** Paste URLs from video sites (like YouTube) to trigger automatic local downloads.
    - **File Explorer:** Copy and paste files just as you would in Windows Explorer or KDE Dolphin.
    - **Web Integration:** Copy images directly from your browser and paste them into the grid.
- 📂 **Board & Data Management:** Your work is organized within a central Data Base directory:
    - **The Data Base:** All files across all boards are stored in this directory, ensuring your assets are always local and accessible.
    - **Quick Switch:** Use the Boards top menu to instantly jump between different board files within your current database.
    - **New Board Wizard:** A dedicated setup tool to help you create and configure new boards with ease.Interface & Persistence
- ✨ **Top Menu:** While shortcuts are available for power users, the Top Menu provides easy access to mode switching, board customization, and visual settings.
- 💾 **Auto-Save:** Focus on your work without worry—every action is automatically saved to the board file in real-time.

### 🖱 Mouse & Navigation
| Action | Shortcut | Description |
| :--- | :--- | :--- |
| **Pan** | `Mid Click` / `Shift+LMB` | Middle Click + drag or Shift + Left Click + drag. |
| **Zoom** | `Wheel` / `Mid+LMB` | Scroll to zoom at pointer; or Mid + LMB drag up/down (Nuke style). |
| **Reset View** | `Home` | Reset zoom level and center the board. |
| **Board View** | `F` | Show every item currently on the board. |
| **Focus Item** | `Double-click` | Fill the screen with selected image, video, or text cell. |
| **Focus Select** | `Shift + F` | Zoom into current selection. |
| **Open Ext.** | `Shift + Dbl-Click` | Open file in your system’s default application. |

### 🔲 Grid & Selection
| Action | Shortcut | Description |
| :--- | :--- | :--- |
| **Select** | `LMB Drag` | Left Click on empty canvas for marquee selection. |
| **Multi-select** | `Ctrl + Click` | Hold Ctrl while clicking to add/remove items. |
| **Duplicate** | `Alt + LMB Drag` | Drag an item or drawing to clone it instantly. |
| **Delete** | `Del` / `Backspace` | Removes selected or hovered items/drawings. |
| **Clear** | `Esc` | Clears selection or cancels current operation. |

### 📋 Clipboard & I/O
| Action | Shortcut | Description |
| :--- | :--- | :--- |
| **Copy Path** | `Ctrl + C` | Copies the file path (media) or raw text. |
| **Copy Bitmap** | `Ctrl + Shift + C` | Copies the actual image data to clipboard. |
| **Smart Paste** | `Ctrl + V` | **URLs:** Auto-download video; **Files:** Copy to DB; **Images:** Auto-save. |
| **Import** | `Ctrl + I` | Manually browse and import media files. |

### 🎨 Annotation & Drawing
| Action | Shortcut | Description |
| :--- | :--- | :--- |
| **Modes** | `Ctrl + 1` / `2` | Switch between Grid/View mode and Draw/Annotation mode. |
| **Visibility** | `Shift + A` | Toggle all annotations on/off. |
| **Text Tool** | `T` | Press `Enter` to commit text, `Esc` to cancel. |
| **Tools** | `V` / `B` / `E` | Select (V), Brush (B), Eraser (E). |
| **Shapes** | `L` / `U` / `O` | Arrow (L), Rectangle (U), Ellipse (O). |

### 💾 General Commands
| Action | Shortcut | Description |
| :--- | :--- | :--- |
| **File** | `Ctrl + N` / `O` / `S` | New Board, Open Board, or Save (As) Board. |
| **History** | `Ctrl + Z` / `Y` | Undo or Redo (also `Ctrl + Shift + Z`). |
| **Stay on Top** | `Ctrl + Shift + T` | Toggles "Always on Top" window state. |
 
<br>
 
# ✨ Key Features
### 📐 Clean Arrangement
- **Grid-Locked:** Every item, label, and backdrop snaps to a global grid.
- **No Overlaps:** The board prevents objects from stacking, maintaining a perfect layout at all times.
- **Backdrops & Labels:** Create visual frames and titles to categorize your workflow.
- **Smart Annotations:** Draw notes that automatically snap to the items they are referencing.
- **Total Order:** No overlapping items. No messy piles. Every object occupies its own dedicated tiles on the grid.
### 📥 Effortless Importing
- **Video Downloader:** Drop a link from popular video sites, and the app fetches the file to your HDD automatically.
- **Universal Input:** Paste images, copy/paste files from your file manager, or drag-and-drop links and text.
- **Supported Types:** Native support for **Images, Videos, and Text files.**
### 🖱 Workflow-First Tools
- **Pipeline Ready:** Right-click to "Open Directory," "Copy Image," or "Copy Path" for instant use in other software.  
- **Always on Top:** Keep your board visible while working in your primary application.  
- **Sorting:** Automatically re-sort selected elements to keep the grid tight.
- **Customization:** Selectable backgrounds and annotation visual effects.
 
<br>

# 🗺 Plans for the Future
This is a personal project made in the "free time of free time," but I hope to eventually add:
- [ ] **Built-in Players:** Better handling for video playback within the grid.
- [ ] **UI Modernization:** Introducing toolbars to complement the current right-click menu.
- [ ] **Tablet Support:** Pressure sensitivity for smoother sketching/annotations.
- [ ] **New Formats:** Support for PDFs, Audio files, and live Webpages.
- [ ] **View Modes:** "View Only" and a specialized "Reference Mode."
- [ ] **Markdown Support:** For Items (Text, Label, Backdrop) and Annotation Text
 
<br>
 
# 📝 A Note on Development
This app is a labor of love and a work in progress. While I have implemented many optimizations to keep the experience smooth, the primary focus is on the logic of the grid and the reliability of the local file system. It is also my first attempt to Coding Agents supported development and... it's kinda cool!  
_I have a lot of plans... we will see... we will see..._
 
<br>
 
# 📦 Dependencies
This application bundles the following third-party binaries to provide core functionality:
* **[FFmpeg](https://ffmpeg.org/)**: Used for audio/video transcoding. Licensed under [LGPLv2.1](https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html) (or GPL depending on your build).
* **[yt-dlp](https://github.com/yt-dlp/yt-dlp)**: Used for video downloading. Licensed under the [Unlicense](https://unlicense.org/).
 
<br>
 
# 📜 License
This project is licensed under the **MIT License**.
Copyright (c) 2026 [Your Name/Github Username]
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software...
