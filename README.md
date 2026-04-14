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

# 👋 Getting Started
### Basic Concept
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

### Controls & Shortcuts
- 🖱 Mouse & Navigation
    - **Pan:** Middle _`Click`_ + _`drag`_ or `Shift` + `Left Click` + _`drag`_.
    - **Zoom:** `Mouse Wheel` to zoom at pointer, or `Middle` + `Left Click` + _`drag up/down`_ (Nuke like).
    - **Reset View:** Press `Home` to reset zoom and center.
    - **Board View:** Press `F` to show every item on the board. 
    - **Focus Item:** `Double-click` an image, video, or text cell to fill the screen.
    - **Focus Selection:** `Shift` + `F` with selected item(s)
    - **External Open:** `Shift` + `Double-click` to open the file in your system’s default application.
- 🔲 Grid & Cell Selection
    - **Select:** `Left Click` on empty canvas for marquee selection.
    - **Multi-select:** `Hold Ctrl` while clicking to add/remove items.
    - **Duplicate:** `Alt` + `Left Click` + _`drag`_ on an item/draw to clone it instantly.
    - **Delete:** `Delete` or `Backspace` removes selected (or hovered) items/draw.
    - **Clear:** `Escape` clears selection or cancels the current operation.
📋 Clipboard & I/O
    - **Copy Path/Text:** `Ctrl` + `C` copies the file path (image/video) or the raw text.
    - **Copy Bitmap:** `Ctrl` + `Shift` + `C` copies the actual image to your clipboard.
    - **Smart Paste:** `Ctrl` + `V` handles everything:
        - **URLs:** Links (like YouTube) trigger automatic video downloads to your HDD.
        - **Files (supported):** Pasting files from your OS copies them into your local database.
        - **Images:** Pasted bitmap data is saved as a new image file automatically.
    - **Import:** `Ctrl` + `I` to manually browse and import media.
- 🎨 Annotation & Drawing
    - **Mode Switch:**
        - `Ctrl` + `1` for Grid/View mode.
        - `Ctrl` + `2` for Draw/Annotation mode.
    - **Visibility:** `Shift` + `A` to toggle all annotations on/off.
    - **Tools:**
        - `V` - Move / Select
        - `B` - Brush
        - `E` - Eraser
        - `T` - Text (Press Enter to commit text, Esc to cancel)
        - `L` - Arrow
        - `U` - Rectangle
        - `O` - Ellipse
- 💾 General Commands
    - **File:**
        - `Ctrl` + `N` - New Board
        - `Ctrl` + `O` - Open Board
        - `Ctrl` + `S` - Save (As) Board
    - **History:**
        - `Ctrl` + `Z` - Undo
        - `Ctrl` + `Y` or `Ctrl` + `Shift` + `Z` - Redo
    - **Always on Top:** `Ctrl` + `Shift` + `T`.
 
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
-   [ ] **Built-in Players:** Better handling for video playback within the grid.
-   [ ] **UI Modernization:** Introducing toolbars to complement the current right-click menu.
-   [ ] **Tablet Support:** Pressure sensitivity for smoother sketching/annotations.
-   [ ] **New Formats:** Support for PDFs, Audio files, and live Webpages.
-   [ ] **View Modes:** "View Only" and a specialized "Reference Mode."
 
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
