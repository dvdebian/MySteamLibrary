# Steam Game Collection Manager

A high-performance **WPF (C#)** desktop application designed to visualize your Steam library with a cinematic flair. It provides a fluid, interactive experience for browsing thousands of games using local caching and optimized search algorithms.



## 🚀 Key Features

* **Steam API Integration**: Connects directly to Steam Web Services to fetch your owned games, playtimes, and metadata.
* **Dual-View Architecture**:
    * **Grid Mode**: Optimized for high-density browsing and quick selection.
    * **Cover Mode**: A horizontal "Cover Flow" experience featuring smooth, eased animations and auto-centering.
* **Performance Optimization**: 
    * **Asynchronous Loading**: Images and data are fetched on background threads to keep the UI at 60 FPS.
    * **Debounced Search**: Uses a timer-based search to prevent UI stutters while typing.
* **Advanced Caching System**: 
    * Reduces API overhead by storing game metadata in `games_cache.json`.
    * Downloads and persists artwork locally in `/ImageCache` to enable instant subsequent loads.
* **Dynamic Image Fallback**: Automatically cascades through Library Art, Header Images, and Icons if specific assets are missing on Steam's servers.

---

## 🛠️ Setup & Requirements

### Prerequisites
* **.NET 6.0 SDK** or higher.
* **Visual Studio 2022** (recommended).
* **Steam API Key**: Generate your key at [Steam Community Dev](https://steamcommunity.com/dev/apikey).
* **SteamID64**: Your unique 17-digit numeric identifier.

### Installation
1. Clone the repository to your local machine.
2. Open the `.sln` file in Visual Studio.
3. Place your `.gitignore` in the root folder.
4. Restore NuGet packages and build the solution (**Ctrl+Shift+B**).
5. Run the application (**F5**).
6. Navigate to **Settings** (gear icon) and input your credentials.

---

## 🧠 Technical Overview

### The Centering Logic
In Cover Mode, the app uses a custom `DependencyProperty` to bridge the gap between WPF's animation system and the `ScrollViewer`. It calculates the precise offset using:

$$TargetOffset = RelativePosition.X - (ViewportWidth / 2) + (ContainerWidth / 2)$$

### Data Management
The application implements `INotifyPropertyChanged` on the `SteamGame` model, ensuring that as background tasks (like description fetching or image caching) complete, the UI updates instantly without requiring a manual refresh.

---

## 🤖 Built With AI (Blame Google, not me)
This project was developed and refined with the assistance of **Gemini 1.5 Flash**, an AI model by Google. Gemini helped in:
* Architectural design and C# logic implementation.
* UI/UX optimization for the WPF environment.
* Code documentation and professional project structuring.

---

## ⚖️ License

**Copyright (C) 2025**

This program is free software: you can redistribute it and/or modify it under the terms of the **GNU General Public License as published by the Free Software Foundation, either version 3 of the License**, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the [GNU General Public License](https://www.gnu.org/licenses/gpl-3.0.html) for more details.