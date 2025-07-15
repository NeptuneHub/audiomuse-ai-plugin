# AudioMuse AI-Plugin - The Jellyfin AudioMuse AI plugin middleware for Developer

AudioMuse-AI-Plugin is a Jellyfin middleware plugin that lets developers interact directly with Jellyfin’s API endpoints—eliminating the need to call the AudioMuse-AI service itself.  

> **Note:** This is an early, backend-only release. Only configuration front-end is included, and some features remain incomplete.


## Versioning TAG
You will always have a specific tag version pluse the laster version. In future the devel tag could also be included.

Here some example:
* **v0.0.1-alpha** (developer preview)  
* **latest** (always points to the newest release)


## Prerequisites

- A running Jellyfin server  
- An existing [AudioMuse-AI](https://github.com/NeptuneHub/AudioMuse-AI) deployment (in its own container)


## Installation and Configuration

* **Create the plugin folder:** Inside your Jellyfin plugin directory, create an `AudioMuseAI` folder. For example, in the linuxserver.io container:
```bash
mkdir -p /data/plugins/AudioMuseAI
cd /data/plugins/AudioMuseAI
````

* **Download the plugin DLL:** 
```bash
#For Latest release:
wget -O Jellyfin.Plugin.AudioMuseAi.dll \
"https://github.com/NeptuneHub/audiomuse-ai-plugin/releases/download/latest/Jellyfin.Plugin.AudioMuseAi.dll"

#For a Specific version (eg. v0.0.1-alpha)
wget -O Jellyfin.Plugin.AudioMuseAi.dll \
"https://github.com/NeptuneHub/audiomuse-ai-plugin/releases/download/v0.0.1-alpha/Jellyfin.Plugin.AudioMuseAi.dll"
```
* **Restart Jellyfin:** Just restart Jellyfin to have it charging the new plugin automatically.
* **Configure Audio-Muse AI endpoint:** Remember to go on the Plugin Configuration Page to add the url of your AudioMuse AI deployment

## Usage

Once Jellyfin is back online, the AudioMuse-AI middleware will be loaded automatically. Your applications can now call Jellyfin’s API endpoints directly—no additional proxying through the AudioMuse-AI service is required.

## Contributing & Feedback

This is an alpha release for testing. Please open issues or pull requests on GitHub to report bugs, request features, or contribute improvements.
