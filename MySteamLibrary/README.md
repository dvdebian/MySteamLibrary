# My Steam Library

A Windows application to browse and search your Steam game collection.
No installation needed.


## 🚀 Key Features

* **Steam API Integration**: Connects directly to Steam Web Services to fetch your owned games, playtimes, and metadata.
* **Multi-View Architecture**:
    * **List Mode**: A vertical list that shows a description when a game is selected.
    * **Grid Mode**: For high-density browsing.
    * **Cover Mode**: A horizontal "Cover Flow" experience.
* **Real-time Search:** Filter your library instantly as you type.
* **Advanced Caching System**: 
    * Reduces API overhead by storing game metadata in `games_cache.json`.
    * Downloads and persists artwork locally in `/ImageCache` to enable instant subsequent loads.
* **Dynamic Image Fallback**: Automatically cascades through Library Art, Header Images, and Icons if specific assets are missing on Steam's servers.
* * **Privacy Focused:** Your Steam ID and API Key are stored locally on your machine.

---

## 🛠️ Requirements

### Prerequisites
* **.NET 6.0 SDK** or higher.
* **Steam API Key**: Generate your key at [Steam Community Dev](https://steamcommunity.com/dev/apikey).
* **SteamID64**: Your unique 17-digit numeric identifier.
* **Visual Studio 2022** (only for developers).

---

## 🚀 Getting Started

To use this application, you will need a **Steam Web API Key**. This allows the app to securely fetch your game list and artwork.

### 1. Get your API Key
1.  Go to the [Steam Community API Key page](https://steamcommunity.com/dev/apikey).
2.  Log in with your Steam account.
3.  Register a domain name (you can just put `localhost`).
4.  Copy your **Key**.

### 2. Find your SteamID64
1.  Go to your Steam Profile.
2.  Right-click anywhere and select **Copy Page URL**.
3.  Use a site like [SteamID.io](https://steamid.io/) to get your **SteamID64** (it's the long number).

### 3. Run the App
1.  Download the latest [Release](https://github.com/YOUR_USERNAME/MySteamLibrary/releases).
2.  Launch `My Steam Library.exe`.
3.  Enter your **API Key** and **SteamID64** into the setup prompts.

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