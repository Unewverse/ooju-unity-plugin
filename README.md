# Ooju Asset Manager

Ooju Asset Manager is a Unity Editor plugin that allows users to upload assets. It supports exporting GameObjects to GLB format using GLTFast and provides an intuitive interface for asset management.

## Dependencies

This package depends on [OOJU Interaction](https://github.com/Unewverse/ooju-unity-interaction) (`com.ooju.interaction`).  
When you install this package, Unity will also attempt to install OOJU Interaction automatically via the Package Manager.

**About OOJU Interaction:**  
OOJU Interaction is a Unity Editor extension that provides AI-powered scene analysis, interaction generation, and animation tools for your Unity projects.  
For more details, see the [OOJU Interaction GitHub repository](https://github.com/Unewverse/ooju-unity-interaction).

---

## Features

- Export GameObjects to `.glb` format using [GLTFast](https://github.com/atteneder/glTFast).
- Manage user authentication and assets in the Unity Editor.

---

## Installation

### Option 1: Using Unity Package Manager

1. Open Unity and go to **Window > Package Manager**.
2. Click the **+** button in the top-left corner and select **Add package from git url**.
3. Enter the following package url: https://github.com/Unewverse/ooju-unity-plugin.git

## How to Use

1. Authenticate Your Account

- Open the Ooju Asset Manager window from tools > OOJU Asset Manager.
- Log in using your account credentials.

2. Uploading Assets

- Select the GameObject in your Unity scene that you want to export.
- Open the Ooju Asset Manager window and click "Export to GLB and Upload".
- The file will be exported and automatically stored in your account.
