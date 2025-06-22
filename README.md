# OOJU Unity Plugin

A comprehensive Unity plugin for asset management and interactive animation systems, providing seamless integration with the OOJU platform.

## Features

### Asset Management
- **Cloud-based Asset Storage**: Upload and download assets to/from OOJU cloud platform
- **Automatic Synchronization**: Real-time sync of modified assets with cloud storage
- **GLB File Support**: Native support for GLB/GLTF files with automatic GLTFast integration
- **Asset Preview Generation**: Automatic thumbnail generation for uploaded assets
- **User Authentication**: Secure login system with token-based authentication
- **Asset Search & Filtering**: Advanced search and filtering capabilities for asset management
- **Batch Operations**: Upload multiple files simultaneously

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
- Unity 6000.0 or later
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


### Network Settings
- Backend URL configuration
- Authentication token management
- Upload/download timeout settings

### LLM Integration
- Support for multiple LLM providers (OpenAI, Claude, Gemini)
- API key configuration
- Custom prompt templates

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

### v1.1.0
- Asset management system
- Interactive scene generation
- Cloud synchronization 
- XR Hand input system