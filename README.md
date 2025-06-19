# OOJU Unity Plugin

A comprehensive Unity plugin for asset management and interactive animation systems, providing seamless integration with the OOJU platform.

## Features

### Asset Management
- **Cloud-based Asset Storage**: Upload and download assets to/from OOJU cloud platform
- **Automatic Synchronization**: Real-time sync of modified assets with cloud storage
- **GLB File Support**: Native support for GLB/GLTF files with automatic GLTFast integration
- **Asset Preview Generation**: Automatic thumbnail generation for uploaded assets
- **User Authentication**: Secure login system with token-based authentication
- **Drag & Drop Upload**: Easy file upload through drag and drop interface
- **Asset Search & Filtering**: Advanced search and filtering capabilities for asset management
- **Batch Operations**: Upload multiple files simultaneously

### Animation System
#### Independent Animations
- **Hover**: Smooth up and down floating motion
- **Wobble**: Gentle side-to-side rocking movement
- **Spin**: Continuous rotation around Y-axis
- **Shake**: Vibrating motion with configurable intensity
- **Bounce**: Spring-like bouncing animation
- **Scale**: Squash and stretch effects

#### Relational Animations
- **Orbit**: Circular motion around a target object
- **LookAt**: Automatic orientation towards a target
- **Follow**: Smooth following behavior with configurable distance
- **MoveAlongPath**: Path-based movement along defined waypoints
- **SnapToObject**: Instant positioning and optional rotation towards target

### Interactive Scene Generation
- **AI-Powered Scene Analysis**: Automatic scene description generation using LLM integration
- **Interaction Suggestion**: AI-generated interaction suggestions for scene objects
- **Natural Language to Code**: Convert natural language descriptions to Unity scripts
- **Player Controller Generation**: Automatic first-person player controller creation
- **Ground Detection**: Automatic ground plane setup for scenes
- **Script Auto-Assignment**: Intelligent script assignment to selected objects

### Development Tools
- **Editor Integration**: Seamless Unity Editor integration with custom UI
- **Coroutine Management**: Efficient animation handling using Unity coroutines
- **Settings Management**: Centralized animation and plugin settings
- **Error Handling**: Comprehensive error handling and user feedback
- **Performance Optimization**: Optimized asset loading and animation processing

## Installation

### Prerequisites
- Unity 2020.3 or later
- Internet connection for cloud features

### Installation Steps
1. Open Unity Package Manager (Window > Package Manager)
2. Click the '+' button and select 'Add package from git URL'
3. Enter: `com.ooju.unityplugin`
4. Click 'Add'

### Dependencies
The plugin will automatically install required dependencies:
- Unity Editor Coroutines
- GLTFast (for GLB/GLTF file support)
- Newtonsoft JSON

## Usage

### Getting Started
1. Open the OOJU Manager: `OOJU > Manager` from Unity's menu bar
2. Navigate between Asset and Interaction tabs
3. Log in with your OOJU account credentials

### Asset Management
1. **Upload Assets**:
   - Drag and drop files into the upload area
   - Or click "Browse Files" to select files manually
   - Supported formats: GLB, GLTF, images, and other Unity-compatible formats

2. **Download Assets**:
   - Browse available assets in the "My Assets" tab
   - Click download button for individual assets
   - Use "Download All" for batch operations

3. **Auto Sync**:
   - Enable auto-sync in settings for automatic cloud synchronization
   - Modified assets in the OOJU_Assets folder will be automatically uploaded

### Animation System
1. **Add Animations to Objects**:
   - Select objects in the scene
   - Choose animation type (Independent or Relational)
   - Configure animation parameters
   - Apply animations using the Animation UI

2. **Independent Animations**:
   - Select animation type (Hover, Wobble, Spin, etc.)
   - Adjust speed, distance, and other parameters
   - Animations run automatically in play mode

3. **Relational Animations**:
   - Choose relational type (Orbit, LookAt, Follow, etc.)
   - Select reference objects or path points
   - Configure relationship parameters

### Interactive Features
1. **Scene Analysis**:
   - Click "Analyze Scene" to generate AI-powered scene descriptions
   - Review suggested interactions for scene objects

2. **Natural Language to Script**:
   - Enter natural language descriptions
   - Generate Unity scripts automatically
   - Scripts are saved and can be assigned to objects

3. **Player Setup**:
   - Add first-person player controller to scene
   - Configure ground detection
   - Set up basic scene navigation

## Configuration

### Animation Settings
All animation parameters can be configured through the `AnimationSettings` class:
- Speed parameters for all animation types
- Distance and magnitude settings
- Duration and timing controls
- Relationship-specific parameters

### Network Settings
- Backend URL configuration
- Authentication token management
- Upload/download timeout settings

### LLM Integration
- Support for multiple LLM providers (OpenAI, Claude, Gemini)
- API key configuration
- Custom prompt templates

## File Structure

```
Assets/
├── Editor/
│   ├── OOJUManager.cs              # Main editor window
│   ├── AssetSyncManager.cs         # Asset synchronization
│   ├── NetworkUtility.cs           # Network operations
│   ├── AssetDownloader.cs          # Asset download handling
│   ├── AnimationUI.cs              # Animation interface
│   └── GltfFastUtility.cs          # GLTF integration
├── Runtime/
│   ├── ObjectAutoAnimator.cs       # Core animation component
│   ├── AnimationSettings.cs        # Animation configuration
│   ├── AnimationType.cs            # Animation type definitions
│   └── RelationalType.cs           # Relational animation types
└── Resources/
    └── ooju_logo.png              # Plugin branding
```

## API Reference

### Core Components

#### ObjectAutoAnimator
Main component for adding animations to GameObjects:
```csharp
// Add to GameObject
ObjectAutoAnimator animator = gameObject.AddComponent<ObjectAutoAnimator>();

// Set independent animation
animator.SetAnimationType(AnimationType.Hover);

// Set relational animation
animator.StartOrbit(targetTransform, radius, speed, duration);
```

#### AnimationSettings
Global animation configuration:
```csharp
AnimationSettings settings = AnimationSettings.Instance;
settings.hoverSpeed = 2.0f;
settings.orbitRadius = 3.0f;
```

### Network Operations
```csharp
// Login
NetworkUtility.Login(username, password, onSuccess, onFailure);

// Upload file
NetworkUtility.UploadFile(filePath, token, onSuccess, onFailure);

// Download assets
AssetDownloader.DownloadAssets(token, onProgress, onComplete, onError);
```

## Troubleshooting

### Common Issues
1. **GLTFast not installed**: Use the "Install GLTFast" button in the manager
2. **Authentication failed**: Check credentials and internet connection
3. **Upload failed**: Verify file format and size limits
4. **Animation not working**: Ensure ObjectAutoAnimator component is attached

### Debug Information
- Check Unity Console for detailed error messages
- Enable debug logging in NetworkUtility for network troubleshooting
- Verify token expiration and refresh if needed

## Support

For technical support and feature requests:
- Email: support@ooju.com
- Website: https://www.ooju.com
- Documentation: Available in the plugin's help system

## License

This plugin is proprietary software owned by OOJU. A valid license is required for commercial use.

## Version History

### v1.0.0
- Initial release
- Asset management system
- Animation framework
- Interactive scene generation
- GLTFast integration
- Cloud synchronization 